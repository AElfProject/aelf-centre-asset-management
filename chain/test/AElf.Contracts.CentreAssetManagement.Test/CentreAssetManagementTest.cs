using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Contracts.TestKit;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace AElf.Contracts.CentreAssetManagement
{
    public class CentreAssetManagementTest : CentreAssetManagementTestBase
    {
        [Fact]
        public async Task HelloCall_ReturnsCentreAssetManagementMessage()
        {
            var txResult = await CentreAssetManagementStub.Hello.SendAsync(new Empty());
            txResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var text = new HelloReturn();
            text.MergeFrom(txResult.TransactionResult.ReturnValue);
            text.Value.ShouldBe("Hello World!");
            var result = await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Symbol = "ELF",
                Amount = 100,
                To = ContractZeroAddress
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }

        [Fact]
        public async Task MainTest()
        {
            //origin address
            //deposit address (exchange virtual user address)
            //vote address (exchange virtual user address)
            //main address (exchange virtual main address)


            AssetMoveDto assetMoveFromVirtualToMainDto1 = new AssetMoveDto()
            {
                Amount = 10_00000000,
                UserToken = "UserToken1",
                HolderId = HolderId
            };

            var userExchangeDepositAddress1 =
                await CentreAssetManagementStub.GetVirtualAddress.CallAsync(assetMoveFromVirtualToMainDto1);

            await TokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                Amount = 10_00000000,
                Symbol = "ELF",
                To = userExchangeDepositAddress1
            });

            await CheckBalanceAsync(userExchangeDepositAddress1, 10_00000000);


            //move elf token to main address
            var moveFromUserExchangeDepositAddress1ToMainAddressResult =
                await CentreAssetManagementStub.MoveAssetToMainAddress.SendAsync(assetMoveFromVirtualToMainDto1);

            Assert.True(moveFromUserExchangeDepositAddress1ToMainAddressResult.Output.Success);

            AssetMoveDto assetMoveFromMainToVirtualTokenLockDto1 = new AssetMoveDto()
            {
                Amount = 5_00000000,
                UserToken = "UserToken1",
                HolderId = HolderId,
                AddressCategoryHash = Hash.FromString("token_lock")
            };


            //move elf token from main address to user1's voting address
            var moveFromMainToVirtualTokenLockResult =
                await CentreAssetManagementStub.MoveAssetFromMainAddress.SendAsync(
                    assetMoveFromMainToVirtualTokenLockDto1);

            Assert.True(moveFromMainToVirtualTokenLockResult.Output.Success);


            var userVoteAddress1 =
                await CentreAssetManagementStub.GetVirtualAddress.CallAsync(assetMoveFromMainToVirtualTokenLockDto1);

            await CheckBalanceAsync(userVoteAddress1, 5_00000000);

            {
                await CentreAssetManagementStub.SendTransactionByUserVirtualAddress.SendAsync(
                    new SendTransactionByUserVirtualAddressDto()
                    {
                        Args = new TransferInput()
                        {
                            Amount = 1_00000000,
                            Symbol = "ELF",
                            To = userExchangeDepositAddress1
                        }.ToByteString(),
                        MethodName = "Transfer",
                        To = TokenContractAddress,
                        HolderId = HolderId,
                        UserToken = "UserToken1",
                        AddressCategoryHash = Hash.FromString("token_lock"),
                    });
            }

            await CheckBalanceAsync(userVoteAddress1, 4_00000000);


            var withdrawAddress1 = Address.FromPublicKey(SampleECKeyPairs.KeyPairs[4].PublicKey);

            //withdraw to user1 origin address
            var requestWithdrawToOriginAddress1Result = await CentreAssetManagementStub.RequestWithdraw.SendAsync(
                new WithdrawRequestDto()
                {
                    Address = withdrawAddress1,
                    Amount = 3_00000000,
                    HolderId = HolderId
                });

            var withdrawRequest1 = requestWithdrawToOriginAddress1Result.Output.Id;

            Assert.True(withdrawRequest1 != null);

            var centreAssetManagementStub2 = GetCentreAssetManagementStub(SampleECKeyPairs.KeyPairs[1]);

            var manageAddress2ApproveWithdraw = await centreAssetManagementStub2.ApproveWithdraw.SendWithExceptionAsync(
                new WithdrawApproveDto()
                {
                    Address = withdrawAddress1, //must keep the same with original withdraw
                    Amount = 3_00000000, //must keep the same with original withdraw,
                    Id = withdrawRequest1
                });

            manageAddress2ApproveWithdraw.TransactionResult.Error.Contains(
                "current management address cannot approve, amount limited");

            var centreAssetManagementStub3 = GetCentreAssetManagementStub(SampleECKeyPairs.KeyPairs[2]);

            var manageAddress3ApproveWithdraw = await centreAssetManagementStub3.ApproveWithdraw.SendAsync(
                new WithdrawApproveDto()
                {
                    Address = withdrawAddress1, //must keep the same with original withdraw
                    Amount = 3_00000000, //must keep the same with original withdraw,
                    Id = withdrawRequest1
                });

            await CheckBalanceAsync(withdrawAddress1, 3_00000000);
        }

        private async Task CheckBalanceAsync(Address userVoteAddress1, long expect)
        {
            var useVoteAddressBalance1 = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = userVoteAddress1,
                Symbol = "ELF"
            });

            Assert.Equal(expect, useVoteAddressBalance1.Balance);
        }


        public Address GetAddress(int index)
        {
            return Address.FromPublicKey(SampleECKeyPairs.KeyPairs[index].PublicKey);
        }

        [Fact]
        public async Task HackAttackAndShutdown()
        {
            var addressOwner = GetAddress(1);
            var shutdownAddress = GetAddress(2);

            var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendAsync(new HolderCreateDto()
            {
                Symbol = "ELF",
                OwnerAddress = addressOwner,
                SettingsEffectiveTime = 5,
                ShutdowAddress = shutdownAddress,
            });

            var holderId = createHolderResult.Output.Id;

            //use OwnerAddress to shutdown
            await GetCentreAssetManagementStub(SampleECKeyPairs.KeyPairs[1]).ShutdownHolder
                .SendAsync(new HolderShutdownDto() {HolderId = holderId});

            await CentreAssetManagementStub.RebootHolder.SendAsync(new HolderRebootDto()
                {HolderOwner = addressOwner, HolderId = holderId});
            
            //use ShutdownAddress to shutdown
            await GetCentreAssetManagementStub(SampleECKeyPairs.KeyPairs[2]).ShutdownHolder
                .SendAsync(new HolderShutdownDto() {HolderId = holderId});

            await CentreAssetManagementStub.RebootHolder.SendAsync(new HolderRebootDto()
                {HolderOwner = addressOwner, HolderId = holderId});
        }

        [Fact]
        public async Task WithdrawCancel()
        {
            var holderInfo = await CentreAssetManagementStub.GetHolderInfo.CallAsync(HolderId);
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Symbol = "ELF",
                To = holderInfo.MainAddress,
                Amount = 1000
            });
            var receiveAddress = GetAddress(4);
            var withdrawResult = await CentreAssetManagementStub.RequestWithdraw.SendAsync(new WithdrawRequestDto
            {
                Amount = 10000,
                Address = receiveAddress,
                HolderId = HolderId
            });
            var withdrawId = withdrawResult.Output.Id;
            await CentreAssetManagementStub.CancelWithdraws.SendAsync(new CancelWithdrawsDto
            {
                HolderId = HolderId,
                Ids = {withdrawId}
            });
            var approveResult = await GetCentreAssetManagementStub(SampleECKeyPairs.KeyPairs[1]).ApproveWithdraw
                .SendWithExceptionAsync(new WithdrawApproveDto
                {
                    Address = receiveAddress,
                    Amount = 10000,
                    Id = withdrawId
                });
            approveResult.TransactionResult.Error.Contains("withdraw not exists");
            
        }

        [Fact]
        public async Task UpdateHolderInfo()
        {
            var addressOwner = GetAddress(0);
            var newOwner = GetAddress(4);
            var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendAsync(new HolderCreateDto()
            {
                Symbol = "ELF",
                OwnerAddress = addressOwner,
                SettingsEffectiveTime = 1,
                ShutdowAddress = addressOwner,
            });
            var holderId = createHolderResult.Output.Id;
            var updateInfo = new HolderUpdateRequestDto
            {
                HolderId = holderId,
                OwnerAddress = newOwner,
                ShutdownAddress = newOwner,
                ManagementAddresses =
                {
                    new ManagementAddress
                    {
                        Amount = 1000,
                        Address = newOwner
                    }
                }
            };
            await CentreAssetManagementStub.RequestUpdateHolder.SendAsync(updateInfo);
            var approveInEffectiveTime = await CentreAssetManagementStub.ApproveUpdateHolder.SendWithExceptionAsync(
                new HolderUpdateApproveDto
                {
                    HolderId = holderId
                });
            approveInEffectiveTime.TransactionResult.Error.Contains("effective time not arrives");
            
            Thread.Sleep(1000);
            await CentreAssetManagementStub.ApproveUpdateHolder.SendAsync(
                new HolderUpdateApproveDto
                {
                    HolderId = holderId
                });
            var updateHolderInfo = await CentreAssetManagementStub.GetHolderInfo.CallAsync(holderId);
            updateHolderInfo.ManagementAddresses.First().Value.Amount.ShouldBe(1000);
        }
    }
}