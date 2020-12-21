using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.ContractTestKit;
using AElf.Types;
using Google.Protobuf;
using Shouldly;
using Xunit;

namespace AElf.Contracts.CentreAssetManagement
{
    public class CentreAssetManagementTest : CentreAssetManagementTestBase
    {
        [Fact]
        public async Task CreateHolderTest()
        {
            DeployContracts();
            await InitializeCentreAssetManagementAsync();

            {
                var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendWithExceptionAsync(
                    new HolderCreateDto()
                    {
                        Symbol = "ELF",
                        ManagementAddresses =
                        {
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[0].KeyPair.PublicKey),
                                Amount = 100,
                                ManagementAddressesLimitAmount = 100,
                                ManagementAddressesInTotal = 2
                            },
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[1].KeyPair.PublicKey),
                                Amount = 10,
                                ManagementAddressesLimitAmount = 10,
                                ManagementAddressesInTotal = 2
                            }
                        }
                    });

                createHolderResult.TransactionResult.Error.ShouldContain("Invalid management address.");
            }
            
            {
                await CentreAssetManagementStub.CreateHolder.SendAsync(
                    new HolderCreateDto()
                    {
                        Symbol = "ELF",
                        ManagementAddresses =
                        {
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[0].KeyPair.PublicKey),
                                Amount = 100,
                                ManagementAddressesLimitAmount = 100,
                                ManagementAddressesInTotal = 2
                            },
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[1].KeyPair.PublicKey),
                                Amount = 100,
                                ManagementAddressesLimitAmount = 10,
                                ManagementAddressesInTotal = 2
                            }
                        }
                    });
            }
        }

        [Fact]
        public async Task MainTest()
        {
            InitializeContracts();

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
                AddressCategoryHash = HashHelper.ComputeFrom("token_lock")
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
                        AddressCategoryHash = HashHelper.ComputeFrom("token_lock"),
                    });
            }

            await CheckBalanceAsync(userVoteAddress1, 4_00000000);


            var withdrawAddress1 = Address.FromPublicKey(SampleAccount.Accounts[4].KeyPair.PublicKey);

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

            var centreAssetManagementStub2 = GetCentreAssetManagementStub(SampleAccount.Accounts[1].KeyPair);

            var manageAddress2ApproveWithdraw = await centreAssetManagementStub2.ApproveWithdraw.SendWithExceptionAsync(
                new WithdrawApproveDto()
                {
                    Address = withdrawAddress1, //must keep the same with original withdraw
                    Amount = 3_00000000, //must keep the same with original withdraw,
                    Id = withdrawRequest1
                });

            manageAddress2ApproveWithdraw.TransactionResult.Error.Contains(
                "current management address cannot approve, amount limited");

            var centreAssetManagementStub3 = GetCentreAssetManagementStub(SampleAccount.Accounts[2].KeyPair);

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
            return Address.FromPublicKey(SampleAccount.Accounts[index].KeyPair.PublicKey);
        }

        [Fact]
        public async Task HackAttackAndShutdown()
        {
            InitializeContracts();
            var addressOwner = GetAddress(1);
            var shutdownAddress = GetAddress(2);

            var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendAsync(new HolderCreateDto()
            {
                Symbol = "ELF",
                OwnerAddress = addressOwner,
                SettingsEffectiveTime = 5,
                ShutdownAddress = shutdownAddress,
            });

            var holderId = createHolderResult.Output.Id;

            //use OwnerAddress to shutdown
            await GetCentreAssetManagementStub(SampleAccount.Accounts[1].KeyPair).ShutdownHolder
                .SendAsync(new HolderShutdownDto() {HolderId = holderId});

            await CentreAssetManagementStub.RebootHolder.SendAsync(new HolderRebootDto()
                {HolderOwner = addressOwner, HolderId = holderId});

            //use ShutdownAddress to shutdown
            await GetCentreAssetManagementStub(SampleAccount.Accounts[2].KeyPair).ShutdownHolder
                .SendAsync(new HolderShutdownDto() {HolderId = holderId});

            await CentreAssetManagementStub.RebootHolder.SendAsync(new HolderRebootDto()
                {HolderOwner = addressOwner, HolderId = holderId});
        }
    }
}