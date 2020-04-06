using System.IO;
using System.Linq;
using Acs0;
using AElf.Blockchains.BasicBaseChain.ContractNames;
using AElf.Contracts.TestKit;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Types;
using Google.Protobuf;
using Volo.Abp.Threading;

namespace AElf.Contracts.CentreAssetManagement
{
    public class CentreAssetManagementTestBase : ContractTestBase<CentreAssetManagementTestModule>
    {
        internal CentreAssetManagementContainer.CentreAssetManagementStub CentreAssetManagementStub { get; set; }
        private ACS0Container.ACS0Stub ZeroContractStub { get; set; }

        private Address CentreAssetManagementAddress { get; set; }

        protected CentreAssetManagementTestBase()
        {
            InitializeContracts();
        }

        private void InitializeContracts()
        {
            ZeroContractStub = GetZeroContractStub(SampleECKeyPairs.KeyPairs.First());

            CentreAssetManagementAddress = AsyncHelper.RunSync(() =>
                ZeroContractStub.DeploySystemSmartContract.SendAsync(
                    new SystemContractDeploymentInput
                    {
                        Category = KernelConstants.DefaultRunnerCategory,
                        Code = ByteString.CopyFrom(File.ReadAllBytes(typeof(CentreAssetManagement).Assembly.Location)),
                        Name = ProfitSmartContractAddressNameProvider.Name,
                        TransactionMethodCallList =
                            new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList()
                    })).Output;
            CentreAssetManagementStub = GetCentreAssetManagementStub(SampleECKeyPairs.KeyPairs.First());
        }

        private ACS0Container.ACS0Stub GetZeroContractStub(ECKeyPair keyPair)
        {
            return GetTester<ACS0Container.ACS0Stub>(ContractZeroAddress, keyPair);
        }

        private CentreAssetManagementContainer.CentreAssetManagementStub GetCentreAssetManagementStub(ECKeyPair keyPair)
        {
            return GetTester<CentreAssetManagementContainer.CentreAssetManagementStub>(CentreAssetManagementAddress, keyPair);
        }
    }
}