using System;
using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.Collections;
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
        private const long WITHDRAW_EXPIRED_SECONDS = 86400;

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
            holderInfo.ShutdownAddress = input.ShutdowAddress;
            holderInfo.SettingsEffectiveTime = input.SettingsEffectiveTime;

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
            Assert(holderId?.Value.IsEmpty == false, "holder id required");

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

        private ManagementAddress CheckMoveFromMainPermission(HolderInfo holderInfo, long amount)
        {
            var managementAddress = GetManagementAddressFromHolderInfo(holderInfo);

            Assert(managementAddress.Amount >= amount,
                "current management address can not move this asset, more amount required");

            return managementAddress;
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
            var virtualUserAddress = GetVirtualUserAddress(input.HolderId, input.UserToken, input.AddressCategoryHash);

            return virtualUserAddress;
        }

        private Hash GetVirtualUserAddress(Hash holder, string userToken, Hash category)
        {
            var virtualUserAddress = Hash.FromString(userToken);

            if (category?.Value != null)
            {
                var map = State.CategoryToContractCallWhiteListsMap[category];
                Assert(map.List.Count > 0, "this category have no contract call list, maybe not initialized.");

                virtualUserAddress = virtualUserAddress.Xor(category);
            }

            return Hash.FromTwoHashes(holder, virtualUserAddress);
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

        public override WithdrawRequestReturnDto RequestWithdraw(WithdrawRequestDto input)
        {
            Assert(!input.Address.Value.IsEmpty, "address required");
            Assert(input.Amount > 0, "amount required");


            var holderInfo = GetHolderInfo(input.HolderId);

            var managementAddress = CheckMoveFromMainPermission(holderInfo, input.Amount);

            Assert(managementAddress.ManagementAddressesInTotal > 0, "current key cannot make withdraw request");


            var withdrawId = Hash.FromTwoHashes(Context.TransactionId, Context.PreviousBlockHash);


            Assert(State.Withdraws[withdrawId] == null, "withdraw already exists");

            State.Withdraws[withdrawId] = new WithdrawInfo()
            {
                ApprovedAddresses = {managementAddress.Address},
                TotalRequired = managementAddress.ManagementAddressesInTotal,
                Address = input.Address,
                Amount = input.Amount,
                HolderId = input.HolderId,
                ManagementAddressesLimitAmount = managementAddress.ManagementAddressesLimitAmount,
                AddedTime = Context.CurrentBlockTime
            };
            WithdrawRequestReturnDto result = new WithdrawRequestReturnDto();

            result.Id = withdrawId;

            return result;
        }

        public override WithdrawApproveReturnDto ApproveWithdraw(WithdrawApproveDto input)
        {
            WithdrawApproveReturnDto.Types.Status status = WithdrawApproveReturnDto.Types.Status.Approving;

            var withdraw = State.Withdraws[input.Id];

            Assert(withdraw != null, "withdraw not exists");

            Assert(withdraw.Amount == input.Amount && withdraw.Address == input.Address, "data not matches");

            var holderInfo = GetHolderInfo(withdraw.HolderId);

            var managementAddress = GetManagementAddressFromHolderInfo(holderInfo);


            Assert(managementAddress.Amount >= withdraw.ManagementAddressesLimitAmount,
                "current management address cannot approve, amount limited");


            var duration = Context.CurrentBlockTime - withdraw.AddedTime;
            if (duration.Seconds >= WITHDRAW_EXPIRED_SECONDS)
            {
                status = WithdrawApproveReturnDto.Types.Status.Expired;
                State.Withdraws.Remove(input.Id);
                return new WithdrawApproveReturnDto()
                {
                    ApprovedAddresses = withdraw.ApprovedAddresses.Count,
                    TotalRequired = withdraw.TotalRequired,
                    Status = status
                };
            }


            if (withdraw.ApprovedAddresses.All(p => p != managementAddress.Address))
            {
                withdraw.ApprovedAddresses.Add(managementAddress.Address);
            }

            if (withdraw.ApprovedAddresses.Count >= withdraw.TotalRequired)
            {
                var tokenInput = new TransferInput()
                {
                    To = withdraw.Address,
                    Amount = withdraw.Amount,
                    Symbol = holderInfo.Symbol
                };
                Context.SendVirtualInline(withdraw.HolderId, State.TokenContract.Value,
                    nameof(State.TokenContract.Transfer), tokenInput);

                State.Withdraws.Remove(input.Id);
                status = WithdrawApproveReturnDto.Types.Status.Approved;
            }

            return new WithdrawApproveReturnDto()
            {
                ApprovedAddresses = withdraw.ApprovedAddresses.Count,
                TotalRequired = withdraw.TotalRequired,
                Status = status
            };
        }

        public override Empty CancelWithdraws(CancelWithdrawsDto input)
        {
            var holderInfo = GetHolderInfo(input.HolderId);
            CheckManagementAddressPermission(holderInfo);

            foreach (var withdrawId in input.Ids)
            {
                var withdraw = State.Withdraws[withdrawId];
                if (withdraw != null)
                {
                    Assert(input.HolderId == withdraw.HolderId);
                    State.Withdraws.Remove(withdrawId);
                }
            }

            return new Empty();
        }

        public override Empty SendTransactionByUserVirtualAddress(SendTransactionByUserVirtualAddressDto input)
        {
            var methodCallWithList = State.CategoryToContractCallWhiteListsMap[input.AddressCategoryHash];

            Assert(methodCallWithList != null, "category not exists");

            var callPermission = methodCallWithList.List.Any(
                p => p.Address == input.To && p.MethodNames.Any(m => m == input.MethodName));

            Assert(callPermission, "cannot invoke this transaction");

            var holderInfo = GetHolderInfo(input.HolderId);
            CheckManagementAddressPermission(holderInfo);

            var virtualUserAddress = GetVirtualUserAddress(
                input.HolderId, input.UserToken, input.AddressCategoryHash);

            Context.SendVirtualInline(virtualUserAddress, input.To,
                input.MethodName, input.Args);

            return new Empty();
        }

        public override Empty RebootHolder(HolderRebootDto input)
        {
            Assert(State.CentreAssetManagementInfo.Value?.Owner == Context.Sender, "no permission");

            var holderInfo = GetHolderInfo(input.HolderId);

            holderInfo.IsShutdown = false;
            holderInfo.ManagementAddresses.Clear();
            holderInfo.UpdatingInfo = null;
            holderInfo.OwnerAddress = input.HolderOwner;
            State.HashToHolderInfoMap[input.HolderId] = holderInfo;
            return new Empty();
        }

        public override Empty RequestUpdateHolder(HolderUpdateRequestDto input)
        {
            var holderInfo = GetHolderInfo(input.HolderId);

            Assert(holderInfo.OwnerAddress == Context.Sender, "no permission");

            holderInfo.UpdatingInfo = new HolderUpdatingInfo()
            {
                ManagementAddresses = {input.ManagementAddresses},
                OwnerAddress = input.OwnerAddress,
                ShutdownAddress = input.ShutdownAddress,
                SettingsEffectiveTime = input.SettingsEffectiveTime,
                UpdatedDate = Context.CurrentBlockTime
            };
            State.HashToHolderInfoMap[input.HolderId] = holderInfo;
            return new Empty();
        }

        public override Empty ShutdownHolder(HolderShutdownDto input)
        {
            var holderInfo = GetHolderInfo(input.HolderId);

            Assert(holderInfo.ShutdownAddress == Context.Sender || holderInfo.OwnerAddress == Context.Sender,
                "no permission");

            holderInfo.IsShutdown = true;
            holderInfo.UpdatingInfo = null;
            State.HashToHolderInfoMap[input.HolderId] = holderInfo;
            return new Empty();
        }

        public override Empty ApproveUpdateHolder(HolderUpdateApproveDto input)
        {
            var holderInfo = GetHolderInfo(input.HolderId);

            Assert(holderInfo.OwnerAddress == Context.Sender, "no permission");

            Assert(Context.CurrentBlockTime >= holderInfo.UpdatingInfo.UpdatedDate +
                new Duration() {Seconds = holderInfo.SettingsEffectiveTime}, "effective time not arrives");


            var updateInfo = holderInfo.UpdatingInfo;

            holderInfo.UpdatingInfo = null;

            holderInfo.SettingsEffectiveTime = updateInfo.SettingsEffectiveTime;
            holderInfo.OwnerAddress = updateInfo.OwnerAddress;
            holderInfo.ShutdownAddress = updateInfo.ShutdownAddress;

            holderInfo.ManagementAddresses.Clear();

            foreach (var managementAddress in updateInfo.ManagementAddresses)
            {
                holderInfo.ManagementAddresses[managementAddress.Address.Value.ToBase64()] = managementAddress;
            }

            State.HashToHolderInfoMap[input.HolderId] = holderInfo;
            return new Empty();
        }

        private void AssertHolderInfoActivated(HolderInfo holderInfo)
        {
            //Assert(Context.CurrentBlockTime >= holderInfo.CreateTIme + holderInfo.SettingsEffectiveTime, "holder is not activated");
            Assert(!holderInfo.IsShutdown, "holder has been shut down");
        }

        [View]
        public override CategoryContractCallAllowanceDto GetCategoryContractCallAllowance(CategoryDto input)
        {
            CategoryContractCallAllowanceDto result = new CategoryContractCallAllowanceDto()
            {
                Category = input.Category,
                List = {State.CategoryToContractCallWhiteListsMap[Hash.FromString(input.Category)].List}
            };
            return result;
        }
    }
}