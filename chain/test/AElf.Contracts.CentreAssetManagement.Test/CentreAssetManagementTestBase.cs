using System.IO;
using System.Linq;
using Acs0;
using AElf.Blockchains.BasicBaseChain.ContractNames;
using AElf.Contracts.MultiToken;
using AElf.Contracts.TestKit;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.Token;
using AElf.Types;
using Google.Protobuf;
using Volo.Abp.Threading;

namespace AElf.Contracts.CentreAssetManagement
{
    public class CentreAssetManagementTestBase : ContractTestBase<CentreAssetManagementTestModule>
    {
        internal CentreAssetManagementContainer.CentreAssetManagementStub CentreAssetManagementStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
        private ACS0Container.ACS0Stub ZeroContractStub { get; set; }

        private Address CentreAssetManagementAddress { get; set; }
        private Address TokenContractAddress { get; set; }

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
            
            TokenContractAddress = AsyncHelper.RunSync(() =>
                ZeroContractStub.DeploySystemSmartContract.SendAsync(
                    new SystemContractDeploymentInput
                    {
                        Category = KernelConstants.DefaultRunnerCategory,
                        Code = ByteString.CopyFrom(File.ReadAllBytes(typeof(TokenContract).Assembly.Location)),
                        Name = TokenSmartContractAddressNameProvider.Name,
                        TransactionMethodCallList = GetTokenContractInitialMethodCallList()
                    })).Output;
            TokenContractStub = GetTokenContractStub(SampleECKeyPairs.KeyPairs.First());
        }

        private ACS0Container.ACS0Stub GetZeroContractStub(ECKeyPair keyPair)
        {
            return GetTester<ACS0Container.ACS0Stub>(ContractZeroAddress, keyPair);
        }

        private CentreAssetManagementContainer.CentreAssetManagementStub GetCentreAssetManagementStub(ECKeyPair keyPair)
        {
            return GetTester<CentreAssetManagementContainer.CentreAssetManagementStub>(CentreAssetManagementAddress, keyPair);
        }
        
        private TokenContractContainer.TokenContractStub GetTokenContractStub(ECKeyPair keyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(CentreAssetManagementAddress, keyPair);
        }

        private SystemContractDeploymentInput.Types.SystemTransactionMethodCallList
            GetTokenContractInitialMethodCallList()
        {
            return new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList
            {
                Value =
                {
                    new SystemContractDeploymentInput.Types.SystemTransactionMethodCall
                    {
                        MethodName = nameof(TokenContractStub.Create),
                        Params = new CreateInput
                        {
                            // Issuer assigned to zero contract in order to issue token after deployment.
                            Issuer = ContractZeroAddress
                        }.ToByteString()
                    },
                    new SystemContractDeploymentInput.Types.SystemTransactionMethodCall
                    {
                        MethodName = nameof(TokenContractStub.Issue),
                        Params = new IssueInput
                        {
                            
                        }.ToByteString()
                    }
                }
            };
        }
    }
}