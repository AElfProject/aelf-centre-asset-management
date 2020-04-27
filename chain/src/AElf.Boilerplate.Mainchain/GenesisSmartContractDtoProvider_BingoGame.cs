using System.Collections.Generic;
using System.Linq;
using Acs0;
using AElf.OS.Node.Application;
using AElf.Contracts.BingoGameContract;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Blockchains.MainChain
{
    public partial class GenesisSmartContractDtoProvider
    {
        public IEnumerable<GenesisSmartContractDto> GetGenesisSmartContractDtosForBingoGame()
        {
            var l = new List<GenesisSmartContractDto>();

            l.AddGenesisSmartContract(
                _codes.Single(kv => kv.Key.Contains("Bingo")).Value,
                HashHelper.ComputeFrom("AElf.ContractNames.BingoGameContract"), GenerateBingoGameInitializationCallList());

            return l;
        }

        private SystemContractDeploymentInput.Types.SystemTransactionMethodCallList
            GenerateBingoGameInitializationCallList()
        {
            var bingoGameContractMethodCallList =
                new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList();
            bingoGameContractMethodCallList.Add(
                nameof(BingoGameContractContainer.BingoGameContractStub.Initial),
                new Empty());
            return bingoGameContractMethodCallList;
        }
    }
}