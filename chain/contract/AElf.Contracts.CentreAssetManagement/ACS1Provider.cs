using AElf.Standards.ACS1;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.CentreAssetManagement
{
    public partial class CentreAssetManagement
    {
        public override Empty ChangeMethodFeeController(AuthorityInfo input)
        {
            if (State.MethodFeeController.Value == null)
            {
                State.MethodFeeController.Value = input;
            }
            else
            {
                Assert(Context.Sender == State.MethodFeeController.Value.OwnerAddress,
                    "Only Owner can change method fee controller.");
            }

            return new Empty();
        }

        public override Empty SetMethodFee(MethodFees input)
        {
            Assert(State.MethodFeeController.Value == null,
                "Need to set method fee controller before setting method fee.");


            Assert(Context.Sender == State.MethodFeeController.Value.OwnerAddress,
                "Only Owner can change method fee controller.");

            State.TransactionFees[input.MethodName] = input;

            return new Empty();
        }

        public override MethodFees GetMethodFee(StringValue input)
        {
            return State.TransactionFees[input.Value];
        }

        public override AuthorityInfo GetMethodFeeController(Empty input)
        {
            return State.MethodFeeController.Value ?? new AuthorityInfo
                {OwnerAddress = State.CentreAssetManagementInfo.Value.Owner};
        }
    }
}