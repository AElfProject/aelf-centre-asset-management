using System.Collections.Generic;
using System.IO;
using System.Linq;
using Acs0;
using AElf.Contracts.MultiToken;
using AElf.Contracts.TestKit;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core;
using AElf.EconomicSystem;
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
        protected Address TokenContractAddress { get; set; }

        private ECKeyPair DefaultKeyPair { get; set; } = SampleECKeyPairs.KeyPairs.First();

        protected Hash HolderId { get; private set; }

        protected CentreAssetManagementTestBase()
        {
            InitializeContracts();
        }

        private void InitializeContracts()
        {
            ZeroContractStub = GetZeroContractStub(DefaultKeyPair);

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
            CentreAssetManagementStub = GetCentreAssetManagementStub(DefaultKeyPair);

            TokenContractAddress = AsyncHelper.RunSync(() =>
                ZeroContractStub.DeploySystemSmartContract.SendAsync(
                    new SystemContractDeploymentInput
                    {
                        Category = KernelConstants.DefaultRunnerCategory,
                        Code = ByteString.CopyFrom(File.ReadAllBytes(typeof(TokenContract).Assembly.Location)),
                        Name = TokenSmartContractAddressNameProvider.Name,
                        TransactionMethodCallList = GetTokenContractInitialMethodCallList()
                    })).Output;
            TokenContractStub = GetTokenContractStub(DefaultKeyPair);

            AsyncHelper.RunSync(async () =>
            {
                await CentreAssetManagementStub.Initialize.SendAsync(
                    new InitializeDto()
                    {
                        Owner = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                        CategoryToContactCallWhiteListsMap =
                        {
                            {
                                "token_lock", new ContractCallWhiteLists()
                                {
                                    List =
                                    {
                                        new ContractCallWhiteList()
                                        {
                                            Address = TokenContractAddress,
                                            MethodNames = {"Lock", "Unlock", "Transfer"}
                                        }
                                    }
                                }
                            }
                        }
                    }
                );

                var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendAsync(new HolderCreateDto()
                {
                    Symbol = "ELF",
                    ManagementAddresses =
                    {
                        new ManagementAddress()
                        {
                            Address = Address.FromPublicKey(SampleECKeyPairs.KeyPairs[0].PublicKey),
                            Amount = long.MaxValue,
                            ManagementAddressesLimitAmount = 1000_000_00000000,
                            ManagementAddressesInTotal = 2
                        },
                        new ManagementAddress()
                        {
                            Address = Address.FromPublicKey(SampleECKeyPairs.KeyPairs[1].PublicKey),
                            Amount = 1000_00000000,
                        },
                        new ManagementAddress()
                        {
                            Address = Address.FromPublicKey(SampleECKeyPairs.KeyPairs[2].PublicKey),
                            Amount = 1000_000_00000000,
                        },
                    }
                });

                HolderId = createHolderResult.Output.Id;
            });
        }

        private ACS0Container.ACS0Stub GetZeroContractStub(ECKeyPair keyPair)
        {
            return GetTester<ACS0Container.ACS0Stub>(ContractZeroAddress, keyPair);
        }

        internal CentreAssetManagementContainer.CentreAssetManagementStub GetCentreAssetManagementStub(
            ECKeyPair keyPair)
        {
            return GetTester<CentreAssetManagementContainer.CentreAssetManagementStub>(CentreAssetManagementAddress,
                keyPair);
        }

        private TokenContractContainer.TokenContractStub GetTokenContractStub(ECKeyPair keyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress, keyPair);
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
                            Issuer = ContractZeroAddress,
                            Symbol = "ELF",
                            IsBurnable = true,
                            Decimals = 8,
                            IsProfitable = true,
                            TokenName = "Elf token.",
                            TotalSupply = 10_0000_0000_00000000
                        }.ToByteString()
                    },
                    new SystemContractDeploymentInput.Types.SystemTransactionMethodCall
                    {
                        MethodName = nameof(TokenContractStub.Issue),
                        Params = new IssueInput
                        {
                            Symbol = "ELF",
                            Amount = 10_0000_0000_00000000,
                            To = Address.FromPublicKey(DefaultKeyPair.PublicKey)
                        }.ToByteString()
                    }
                }
            };
        }
    }
}