using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.CentreAssetManagement
{
    /// <summary>
    /// The C# implementation of the contract defined in centre_asset_management.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public class CentreAssetManagement : CentreAssetManagementContainer.CentreAssetManagementBase
    {
        /// <summary>
        /// The implementation of the Hello method. It takes no parameters and returns on of the custom data types
        /// defined in the protobuf definition file.
        /// </summary>
        /// <param name="input">Empty message (from Protobuf)</param>
        /// <returns>a HelloReturn</returns>
        public override HelloReturn Hello(Empty input)
        {
            return new HelloReturn {Value = "Hello World!"};
        }

        public override HolderCreateReturnDto CreateHolder(HolderCreateDto input)
        {
            var result = new HolderCreateReturnDto();

            HolderInfo holderInfo = new HolderInfo();

            var holderId = Context.TransactionId.Xor(Hash.FromString(input.Name));

            Assert(State.HashToHolderInfoMap[holderId].MainAddress.Value.IsEmpty, "already have a holder");

            holderInfo.MainAddress = GetMainAddress(holderId);
            foreach (var managementAddress in input.ManagementAddresses)
            {
                holderInfo.ManagementAddresses[managementAddress.Address.Value.ToBase64()] = managementAddress;
            }

            holderInfo.OwnerAddress = input.OwnerAddress;

            holderInfo.Name = input.Name;

            holderInfo.MainAddress = GetMainAddress(holderId);

            State.HashToHolderInfoMap[holderId] = holderInfo;

            return result;
        }

        public override Empty Initialize(InitializeDto input)
        {
            Assert(!State.Initialized.Value, "already initialized.");

            foreach (var contractCallWhiteLists in input.CategoryToContactCallWhiteListsMap)
            {
                State.CategoryToContractCallWhiteListsMap.Set(Hash.FromString(contractCallWhiteLists.Key),
                    contractCallWhiteLists.Value);
            }

            State.CentreAssetManagementInfo.Value.Owner = Context.Sender; // TODO: set the owner to something else.

            return new Empty();
        }

        public override AssetMoveReturnDto MoveAssetToMainAddress(AssetMoveDto input)
        {
            AssetMoveReturnDto result = new AssetMoveReturnDto();
            var mainAddress = GetMainAddress(input.HolderId);

            var virtualUserAddress = GetVirtualUserAddress(input);

            var tokenInput = new TransferInput()
            {
                To = mainAddress,
                Amount = input.Amount,
                Symbol = input.Symbol
            };

            State.TokenContract.Transfer.Send(tokenInput);

            result.Success = true;

            return result;
        }

        [View]
        public override Address GetVirtualAddress(AssetMoveDto input)
        {
            return Context.ConvertVirtualAddressToContractAddress(GetVirtualUserAddress(input));
        }

        private Hash GetVirtualUserAddress(AssetMoveDto input)
        {
            var virtualUserAddress = Hash.FromString(input.UserToken);

            if (!input.AddressCategoryHash.Value.IsNullOrEmpty())
            {
                var map = State.CategoryToContractCallWhiteListsMap[input.AddressCategoryHash];
                Assert(map.List.Count > 0, "this category have no contract call list, maybe not initialized.");

                virtualUserAddress = virtualUserAddress.Xor(input.AddressCategoryHash);
            }

            return virtualUserAddress;
        }

        private Address GetMainAddress(Hash holderId)
        {
            return Context.ConvertVirtualAddressToContractAddress(holderId);
        }
    }
}