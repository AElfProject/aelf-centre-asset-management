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
            AssetMoveDto assetMoveDto = new AssetMoveDto()
            {
                Amount = 1,
                Symbol = "ELF",
                UserToken = "UserToken1",
            };
        }
    }
}