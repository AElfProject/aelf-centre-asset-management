using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace AElf.Contracts.CentreAssetManagement
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public class CentreAssetManagementState : ContractState
    {
        // state definitions go here.

        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }


        public SingletonState<CentreAssetManagementInfo> CentreAssetManagementInfo { get; set; }

        //Store what method can be called in one category
        public MappedState<Hash,ContractCallWhiteLists> CategoryToContractCallWhiteListsMap { get; set; }
        
        public SingletonState<bool> Initialized { get; set; }
        
        
        public MappedState<Hash,HolderInfo> HashToHolderInfoMap { get; set; }
        
        public MappedState<Hash,WithdrawInfo> Withdraws { get; set; }
        
    }
}