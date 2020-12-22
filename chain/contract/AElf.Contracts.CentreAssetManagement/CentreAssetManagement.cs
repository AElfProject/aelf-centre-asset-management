using System.Linq;
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
        private const long WITHDRAW_EXPIRED_SECONDS = 86400;

        public override HolderCreateReturnDto CreateHolder(HolderCreateDto input)
        {
            Assert(!string.IsNullOrWhiteSpace(input.Symbol), "Symbol cannot be null or white space.");

            HolderInfo holderInfo = new HolderInfo();

            var holderId = HashHelper.ConcatAndCompute(Context.TransactionId, Context.PreviousBlockHash);

            Assert(State.HashToHolderInfoMap[holderId] == null, "Holder already exists.");

            holderInfo.MainAddress = Context.ConvertVirtualAddressToContractAddress(holderId);
            foreach (var managementAddress in input.ManagementAddresses)
            {
                Assert(!holderInfo.ManagementAddresses.TryGetValue(managementAddress.Address.Value.ToBase64(), out _), "The same management address exists.");
                holderInfo.ManagementAddresses[managementAddress.Address.Value.ToBase64()] = managementAddress;
            }
            
            holderInfo.OwnerAddress = input.OwnerAddress;
            holderInfo.ShutdownAddress = input.ShutdownAddress;
            holderInfo.SettingsEffectiveTime = input.SettingsEffectiveTime;

            var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput() {Symbol = input.Symbol});

            Assert(tokenInfo.Symbol == input.Symbol, "Symbol is not registered in token contract.");

            holderInfo.Symbol = input.Symbol;

            ValidateHolderInfo(holderInfo);
            State.HashToHolderInfoMap[holderId] = holderInfo;

            var result = new HolderCreateReturnDto()
            {
                Id = holderId,
                Info = holderInfo
            };

            Context.Fire(new HolderCreated
            {
                HolderId = holderId,
                Symbol = input.Symbol,
                OwnerAddress = input.OwnerAddress
            });

            return result;
        }

        public override Empty Initialize(InitializeDto input)
        {
            Assert(!State.Initialized.Value, "Already initialized.");
            Assert(input.Owner != null, "Contract owner cannot be null.");

            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);

            foreach (var contractCallWhiteLists in input.CategoryToContactCallWhiteListsMap)
            {
                State.CategoryToContractCallWhiteListsMap.Set(
                    CalculateCategoryHash(new StringValue {Value = contractCallWhiteLists.Key}),
                    contractCallWhiteLists.Value);
            }

            State.CentreAssetManagementInfo.Value = new CentreAssetManagementInfo()
            {
                Owner = input.Owner,
                Categories = {input.CategoryToContactCallWhiteListsMap.Keys}
            };

            State.Initialized.Value = true;

            return new Empty();
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

            AssetMoveReturnDto result = new AssetMoveReturnDto {Success = true};

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
            var virtualUserAddress = HashHelper.ComputeFrom(userToken);

            if (category?.Value != null)
            {
                var map = State.CategoryToContractCallWhiteListsMap[category];
                Assert(map.List.Count > 0, "No contract call list for this category, maybe not initialized.");

                virtualUserAddress = HashHelper.XorAndCompute(virtualUserAddress, category);
            }

            return HashHelper.ConcatAndCompute(holder, virtualUserAddress);
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
            Assert(!input.Address.Value.IsEmpty, "Address required.");
            Assert(input.Amount > 0, "Amount required.");


            var holderInfo = GetHolderInfo(input.HolderId);

            var managementAddress = CheckMoveFromMainPermission(holderInfo, input.Amount);

            Assert(managementAddress.ManagementAddressesInTotal > 0, "Current key cannot make withdraw request.");


            var withdrawId = HashHelper.ConcatAndCompute(Context.TransactionId, Context.PreviousBlockHash);


            Assert(State.Withdraws[withdrawId] == null, "Withdraw already exists.");

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

            Context.Fire(new WithdrawRequested
            {
                Amount = input.Amount,
                HolderId = input.HolderId,
                ReqeustAddress = Context.Sender,
                WithdrawAddress = input.Address
            });

            return result;
        }

        public override WithdrawApproveReturnDto ApproveWithdraw(WithdrawApproveDto input)
        {
            WithdrawApproveReturnDto.Types.Status status = WithdrawApproveReturnDto.Types.Status.Approving;

            var withdraw = State.Withdraws[input.Id];

            Assert(withdraw != null, "Withdraw not exists.");

            Assert(withdraw.Amount == input.Amount && withdraw.Address == input.Address, "Withdraw data not matched.");

            var holderInfo = GetHolderInfo(withdraw.HolderId);

            var managementAddress = GetManagementAddressFromHolderInfo(holderInfo);


            Assert(managementAddress.Amount >= withdraw.ManagementAddressesLimitAmount,
                "Current management address cannot approve, amount limited");


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

            Assert(methodCallWithList != null, "Category not exists.");

            var callPermission = methodCallWithList.List.Any(
                p => p.Address == input.To && p.MethodNames.Any(m => m == input.MethodName));

            Assert(callPermission, "Unable to execute this transaction.");

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
            Assert(State.CentreAssetManagementInfo.Value?.Owner == Context.Sender, "No permission.");

            var holderInfo = GetHolderInfo(input.HolderId);

            holderInfo.IsShutdown = false;
            holderInfo.ManagementAddresses.Clear();
            holderInfo.UpdatingInfo = null;
            holderInfo.OwnerAddress = input.HolderOwner;
            ValidateHolderInfo(holderInfo);
            State.HashToHolderInfoMap[input.HolderId] = holderInfo;
            return new Empty();
        }

        public override Empty RequestUpdateHolder(HolderUpdateRequestDto input)
        {
            var holderInfo = GetHolderInfo(input.HolderId);

            Assert(holderInfo.OwnerAddress == Context.Sender, "No permission.");

            holderInfo.UpdatingInfo = new HolderUpdatingInfo()
            {
                ManagementAddresses = {input.ManagementAddresses},
                OwnerAddress = input.OwnerAddress,
                ShutdownAddress = input.ShutdownAddress,
                SettingsEffectiveTime = input.SettingsEffectiveTime,
                UpdatedDate = Context.CurrentBlockTime
            };
            ValidateUpdatingHolderInfo(holderInfo.UpdatingInfo);
            State.HashToHolderInfoMap[input.HolderId] = holderInfo;
            return new Empty();
        }

        public override Empty ShutdownHolder(HolderShutdownDto input)
        {
            var holderInfo = GetHolderInfo(input.HolderId);

            Assert(holderInfo.ShutdownAddress == Context.Sender || holderInfo.OwnerAddress == Context.Sender,
                "No permission.");

            holderInfo.IsShutdown = true;
            holderInfo.UpdatingInfo = null;
            State.HashToHolderInfoMap[input.HolderId] = holderInfo;
            return new Empty();
        }

        public override Empty ApproveUpdateHolder(HolderUpdateApproveDto input)
        {
            var holderInfo = GetHolderInfo(input.HolderId);

            Assert(holderInfo.OwnerAddress == Context.Sender, "No permission.");

            Assert(Context.CurrentBlockTime >= holderInfo.UpdatingInfo.UpdatedDate +
                new Duration() {Seconds = holderInfo.SettingsEffectiveTime}, "Effective time not arrived.");


            var updateInfo = holderInfo.UpdatingInfo;
            Assert(updateInfo != null, "Updating info not found.");

            holderInfo.UpdatingInfo = null;

            holderInfo.SettingsEffectiveTime = updateInfo.SettingsEffectiveTime;
            holderInfo.OwnerAddress = updateInfo.OwnerAddress;
            holderInfo.ShutdownAddress = updateInfo.ShutdownAddress;

            holderInfo.ManagementAddresses.Clear();

            foreach (var managementAddress in updateInfo.ManagementAddresses)
            {
                holderInfo.ManagementAddresses[managementAddress.Address.Value.ToBase64()] = managementAddress;
            }

            ValidateHolderInfo(holderInfo);
            State.HashToHolderInfoMap[input.HolderId] = holderInfo;
            return new Empty();
        }

        public override Empty AddCategoryToContractCallWhiteLists(CategoryToContractCallWhiteListsDto input)
        {
            Assert(State.CentreAssetManagementInfo.Value?.Owner == Context.Sender, "No permission.");
            var centreAssetManagementInfo = State.CentreAssetManagementInfo.Value;
            foreach (var contractCallWhiteLists in input.CategoryToContactCallWhiteListsMap)
            {
                var categoryHash = CalculateCategoryHash(new StringValue {Value = contractCallWhiteLists.Key});
                if (State.CategoryToContractCallWhiteListsMap[categoryHash] != null)
                    centreAssetManagementInfo.Categories.Add(contractCallWhiteLists.Key);
                State.CategoryToContractCallWhiteListsMap.Set(categoryHash, contractCallWhiteLists.Value);
            }

            State.CentreAssetManagementInfo.Value = centreAssetManagementInfo;

            return new Empty();
        }

        public override Empty ChangeContractOwner(Address input)
        {
            var centreAssetManagementInfo = State.CentreAssetManagementInfo.Value;
            Assert(centreAssetManagementInfo.Owner == Context.Sender, "No permission.");
            centreAssetManagementInfo.Owner = input;
            State.CentreAssetManagementInfo.Value = centreAssetManagementInfo;
            return new Empty();
        }

        [View]
        public override CategoryToContractCallWhiteListsDto GetCategoryToContractCall(Empty input)
        {
            var categoryToContractCallWhiteListsDto = new CategoryToContractCallWhiteListsDto();
            var centreAssetManagementInfo = State.CentreAssetManagementInfo.Value;
            foreach (var category in centreAssetManagementInfo.Categories)
            {
                categoryToContractCallWhiteListsDto.CategoryToContactCallWhiteListsMap[category] =
                    State.CategoryToContractCallWhiteListsMap[GetCategoryHash(new StringValue {Value = category})];
            }

            return categoryToContractCallWhiteListsDto;
        }

        [View]
        public override CategoryContractCallAllowanceDto GetCategoryContractCallAllowance(CategoryDto input)
        {
            CategoryContractCallAllowanceDto result = new CategoryContractCallAllowanceDto
            {
                Category = input.Category,
                List =
                {
                    State.CategoryToContractCallWhiteListsMap[GetCategoryHash(new StringValue {Value = input.Category})]
                        .List
                }
            };
            return result;
        }

        [View]
        public override Hash GetCategoryHash(StringValue category)
        {
            var hash = CalculateCategoryHash(category);
            return State.CategoryToContractCallWhiteListsMap[hash] != null ? hash : null;
        }

        [View]
        public override CentreAssetManagementInfo GetCentreAssetManagementInfo(Empty input)
        {
            return State.CentreAssetManagementInfo.Value;
        }
        
        [View]
        public override HolderInfo GetHolderInfo(Hash holderId)
        {
            Assert(holderId?.Value.IsEmpty == false, "Holder id required.");

            var holderInfo = State.HashToHolderInfoMap[holderId];
            Assert(holderInfo != null, "Holder is not initialized.");
            return holderInfo;
        }

        private Hash CalculateCategoryHash(StringValue category)
        {
            return HashHelper.ComputeFrom(category.Value);
        }

        private void ValidateHolderInfo(HolderInfo holderInfo)
        {
            Assert(holderInfo.OwnerAddress != null, "Owner address cannot be null.");
            Assert(holderInfo.ShutdownAddress != null, "Shutdown address cannot be null.");
            
            Assert(holderInfo.ManagementAddresses.Values.All(managementAddress =>
                holderInfo.ManagementAddresses.Values.Count(m =>
                    m.Amount >= managementAddress.ManagementAddressesLimitAmount) >=
                managementAddress.ManagementAddressesInTotal), "Invalid management address.");
        }
        
        private void ValidateUpdatingHolderInfo(HolderUpdatingInfo holderInfo)
        {
            Assert(holderInfo.OwnerAddress != null, "Owner address cannot be null.");
            Assert(holderInfo.ShutdownAddress != null, "Shutdown address cannot be null.");
            
            Assert(holderInfo.ManagementAddresses.All(managementAddress =>
                holderInfo.ManagementAddresses.Count(m =>
                    m.Amount >= managementAddress.ManagementAddressesLimitAmount) >=
                managementAddress.ManagementAddressesInTotal), "Invalid management address.");
        }
        
        private ManagementAddress GetManagementAddressFromHolderInfo(HolderInfo holderInfo)
        {
            ManagementAddress managementAddress;
            Assert(
                holderInfo.ManagementAddresses.TryGetValue(Context.Sender.Value.ToBase64(), out managementAddress),
                "Sender is not registered as management address in the holder");

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
                "Current management address can not move this asset, more amount required.");

            return managementAddress;
        }
    }
}