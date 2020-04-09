using System;
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

        private void CheckWithdrawPermission(HolderInfo holderInfo, long amount)
        {
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
    }
}