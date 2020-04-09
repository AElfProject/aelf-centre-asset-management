using System;
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
            Assert(!string.IsNullOrWhiteSpace(input.Symbol), "symbol cannot be null or white space");

            HolderInfo holderInfo = new HolderInfo();

            var holderId = Hash.FromTwoHashes(Context.TransactionId, Context.PreviousBlockHash);

            Assert(State.HashToHolderInfoMap[holderId] == null, "already have a holder");

            holderInfo.MainAddress = Context.ConvertVirtualAddressToContractAddress(holderId);
            foreach (var managementAddress in input.ManagementAddresses)
            {
                holderInfo.ManagementAddresses[managementAddress.Address.Value.ToBase64()] = managementAddress;
            }

            holderInfo.OwnerAddress = input.OwnerAddress;

            var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput() {Symbol = input.Symbol});

            Assert(tokenInfo.Symbol == input.Symbol, "symbol is not registered in token contract");

            holderInfo.Symbol = input.Symbol;


            State.HashToHolderInfoMap[holderId] = holderInfo;

            var result = new HolderCreateReturnDto()
            {
                Id = holderId,
                Info = holderInfo
            };


            return result;
        }

        public override Empty Initialize(InitializeDto input)
        {
            Assert(!State.Initialized.Value, "already initialized.");


            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);

            foreach (var contractCallWhiteLists in input.CategoryToContactCallWhiteListsMap)
            {
                State.CategoryToContractCallWhiteListsMap.Set(Hash.FromString(contractCallWhiteLists.Key),
                    contractCallWhiteLists.Value);
            }

            State.CentreAssetManagementInfo.Value = new CentreAssetManagementInfo()
            {
                Owner = input.Owner
            };

            State.Initialized.Value = true;

            return new Empty();
        }


        private HolderInfo GetHolderInfo(Hash holderId)
        {
            var holderInfo = State.HashToHolderInfoMap[holderId];
            Assert(holderInfo != null, "holder is not initialized");

            return holderInfo;
        }


        private ManagementAddress GetManagementAddressFromHolderInfo(HolderInfo holderInfo)
        {
            ManagementAddress managementAddress;
            Assert(
                holderInfo.ManagementAddresses.TryGetValue(Context.Sender.Value.ToBase64(), out managementAddress),
                "sender is not registered as management address in the holder");

            return managementAddress;
        }

        private void CheckManagementAddressPermission(HolderInfo holderInfo)
        {
            GetManagementAddressFromHolderInfo(holderInfo);
        }

        private void CheckMoveFromMainPermission(HolderInfo holderInfo, long amount)
        {
            var managementAddress = GetManagementAddressFromHolderInfo(holderInfo);
            
            Assert(managementAddress.Amount >= amount,
                "current management address can not move this asset, more amount required");
            
        }

        public override AssetMoveReturnDto MoveAssetToMainAddress(AssetMoveDto input)
        {
            var holderInfo = GetHolderInfo(input.HolderId);

            CheckManagementAddressPermission(holderInfo);

            var mainAddress = Context.ConvertVirtualAddressToContractAddress(input.HolderId);

            var virtualUserAddress = GetVirtualUserAddress(input);

            var tokenInput = new TransferInput()
            {
                To = mainAddress,
                Amount = input.Amount,
                Symbol = holderInfo.Symbol
            };

            Context.SendVirtualInline(virtualUserAddress, State.TokenContract.Value,
                nameof(State.TokenContract.Transfer), tokenInput);

            AssetMoveReturnDto result = new AssetMoveReturnDto();
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

            if (input.AddressCategoryHash?.Value != null)
            {
                var map = State.CategoryToContractCallWhiteListsMap[input.AddressCategoryHash];
                Assert(map.List.Count > 0, "this category have no contract call list, maybe not initialized.");

                virtualUserAddress = virtualUserAddress.Xor(input.AddressCategoryHash);
            }

            return virtualUserAddress;
        }

        public override AssetMoveReturnDto MoveAssetFromMainAddress(AssetMoveDto input)
        {
            var holderInfo = GetHolderInfo(input.HolderId);

            CheckMoveFromMainPermission(holderInfo, input.Amount);

            var virtualUserAddress = GetVirtualUserAddress(input);

            var tokenInput = new TransferInput()
            {
                To = Context.ConvertVirtualAddressToContractAddress(virtualUserAddress),
                Amount = input.Amount,
                Symbol = holderInfo.Symbol
            };

            Context.SendVirtualInline(input.HolderId, State.TokenContract.Value,
                nameof(State.TokenContract.Transfer), tokenInput);


            AssetMoveReturnDto result = new AssetMoveReturnDto();

            result.Success = true;

            return result;
        }
    }
}