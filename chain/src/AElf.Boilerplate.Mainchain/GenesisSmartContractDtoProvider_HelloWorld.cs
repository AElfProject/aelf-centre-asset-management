using System.Collections.Generic;
using System.Linq;
using Acs0;
using AElf.OS.Node.Application;

namespace AElf.Blockchains.MainChain
{
    /* Part of the GenesisSmartContractDtoProvider */
    public partial class GenesisSmartContractDtoProvider
    {
        public IEnumerable<GenesisSmartContractDto> GetGenesisSmartContractDtosForCentreAssetManagement()
        {
            var l = new List<GenesisSmartContractDto>();

            l.AddGenesisSmartContract(
                // find the contracts code by name
                _codes.Single(kv => kv.Key.Contains("CentreAssetManagement")).Value,
                // the name of the contract is built from the full name
                HashHelper.ComputeFrom("AElf.ContractNames.CentreAssetManagement"), 
                
                new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList());

            return l;
        }
    }
}