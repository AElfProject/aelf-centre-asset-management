using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
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

            var userDepositAddressBalance1 = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = userExchangeDepositAddress1,
                Symbol = "ELF"
            });

            Assert.Equal(10_00000000, userDepositAddressBalance1.Balance);


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
            
            var useVoteAddressBalance1 = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = userVoteAddress1,
                Symbol = "ELF"
            });

            Assert.Equal(5_00000000, useVoteAddressBalance1.Balance);
        }
    }
}