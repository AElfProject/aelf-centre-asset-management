using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.ContractTestKit;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace AElf.Contracts.CentreAssetManagement
{
    public class CentreAssetManagementTest : CentreAssetManagementTestBase
    {
        [Fact]
        public async Task InitializeTest()
        {
            DeployContracts();

            {
                var initializeResult = await CentreAssetManagementStub.Initialize.SendWithExceptionAsync(
                    new InitializeDto
                    {
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

                initializeResult.TransactionResult.Error.ShouldContain("Contract owner cannot be null.");
            }

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

            var cateGoryHash =
                await CentreAssetManagementStub.GetCategoryHash.CallAsync(new StringValue {Value = "token_lock"});
            cateGoryHash.ShouldNotBeNull();

            {
                var initializeResult = await CentreAssetManagementStub.Initialize.SendWithExceptionAsync(
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

                initializeResult.TransactionResult.Error.ShouldContain("Already initialized.");
            }
        }

        [Fact]
        public async Task GetCentreAssetManagementTest()
        {
            InitializeContracts();
            var centreAssetManagementInfo =
                await CentreAssetManagementStub.GetCentreAssetManagementInfo.CallAsync(new Empty());
            centreAssetManagementInfo.Owner.ShouldBe(Address.FromPublicKey(DefaultKeyPair.PublicKey));
            centreAssetManagementInfo.Categories.Count.ShouldBe(1);
            centreAssetManagementInfo.Categories.ShouldContain("token_lock");
        }

        [Fact]
        public async Task GetHolderInfoTest()
        {
            InitializeContracts();
            var holder =
                await CentreAssetManagementStub.GetHolderInfo.CallAsync(HolderId);
            holder.Symbol.ShouldBe("ELF");
            holder.IsShutdown.ShouldBeFalse();
            holder.ManagementAddresses.Count.ShouldBe(3);
            holder.ManagementAddresses.ShouldContainKey(Address
                .FromPublicKey(SampleAccount.Accounts[0].KeyPair.PublicKey).Value.ToBase64());
            holder.ManagementAddresses.ShouldContainKey(Address
                .FromPublicKey(SampleAccount.Accounts[1].KeyPair.PublicKey).Value.ToBase64());
            holder.ManagementAddresses.ShouldContainKey(Address
                .FromPublicKey(SampleAccount.Accounts[2].KeyPair.PublicKey).Value.ToBase64());
            holder.OwnerAddress.ShouldBe(Address.FromPublicKey(DefaultKeyPair.PublicKey));
            holder.MainAddress.ShouldNotBeNull();
            holder.SettingsEffectiveTime.ShouldBe(3600);
        }

        [Fact]
        public async Task CreateHolderTest()
        {
            DeployContracts();
            await InitializeCentreAssetManagementAsync();

            {
                var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendWithExceptionAsync(
                    new HolderCreateDto()
                    {
                        Symbol = "ELF",
                        ManagementAddresses =
                        {
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[0].KeyPair.PublicKey),
                                Amount = 100,
                                ManagementAddressesLimitAmount = 100,
                                ManagementAddressesInTotal = 2
                            },
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[1].KeyPair.PublicKey),
                                Amount = 10,
                                ManagementAddressesLimitAmount = 10,
                                ManagementAddressesInTotal = 2
                            }
                        },
                        OwnerAddress = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                        ShutdownAddress = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                    });

                createHolderResult.TransactionResult.Error.ShouldContain("Invalid management address.");
            }

            {
                var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendAsync(
                    new HolderCreateDto()
                    {
                        Symbol = "ELF",
                        ManagementAddresses =
                        {
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[0].KeyPair.PublicKey),
                                Amount = 100,
                                ManagementAddressesLimitAmount = 100,
                                ManagementAddressesInTotal = 2
                            },
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[1].KeyPair.PublicKey),
                                Amount = 100,
                                ManagementAddressesLimitAmount = 10,
                                ManagementAddressesInTotal = 2
                            }
                        },
                        OwnerAddress = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                        ShutdownAddress = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                    });

                var logEvent =
                    createHolderResult.TransactionResult.Logs.FirstOrDefault(l => l.Name == nameof(HolderCreated));
                var holderCreated = HolderCreated.Parser.ParseFrom(logEvent.NonIndexed);
                holderCreated.Symbol.ShouldBe("ELF");
                holderCreated.HolderId.ShouldBe(createHolderResult.Output.Id);
                holderCreated.OwnerAddress.ShouldBe(Address.FromPublicKey(DefaultKeyPair.PublicKey));
            }

            {
                var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendWithExceptionAsync(
                    new HolderCreateDto()
                    {
                        Symbol = "ELF",
                        ManagementAddresses =
                        {
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[0].KeyPair.PublicKey),
                                Amount = 100,
                                ManagementAddressesLimitAmount = 100,
                                ManagementAddressesInTotal = 2
                            },
                            new ManagementAddress
                            {
                                Address = Address.FromPublicKey(SampleAccount.Accounts[0].KeyPair.PublicKey),
                                Amount = 100,
                                ManagementAddressesLimitAmount = 10,
                                ManagementAddressesInTotal = 1
                            }
                        },
                        OwnerAddress = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                        ShutdownAddress = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                    });

                createHolderResult.TransactionResult.Error.ShouldContain("The same management address exists");
            }
        }

        [Fact]
        public async Task UserChargingTest()
        {
            InitializeContracts();
            var userChargingAddress1 = await CentreAssetManagementStub.GetVirtualAddress.CallAsync(
                new VirtualAddressCalculationDto
                {
                    AddressCategoryHash =
                        await CentreAssetManagementStub.GetCategoryHash.CallAsync(
                            new StringValue {Value = "token_lock"}),
                    HolderId = HolderId,
                    UserToken = "user1"
                });

            var userChargingAddress2 = await CentreAssetManagementStub.GetVirtualAddress.CallAsync(
                new VirtualAddressCalculationDto
                {
                    HolderId = HolderId,
                    UserToken = "user1"
                });

            userChargingAddress1.ShouldNotBe(userChargingAddress2);
        }

        [Fact]
        public async Task MoveAssetToMainAddressTest()
        {
            InitializeContracts();
            var userChargingAddress1 = await CentreAssetManagementStub.GetVirtualAddress.CallAsync(
                new VirtualAddressCalculationDto
                {
                    AddressCategoryHash =
                        await CentreAssetManagementStub.GetCategoryHash.CallAsync(
                            new StringValue {Value = "token_lock"}),
                    HolderId = HolderId,
                    UserToken = "user1"
                });

            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Amount = 10_00000000,
                Symbol = "ELF",
                To = userChargingAddress1
            });


            var centreAssetManagementStub1 = GetCentreAssetManagementStub(SampleAccount.Accounts[5].KeyPair);

            //move elf token to main address
            {
                var moveFromUserExchangeDepositAddress1ToMainAddressResult =
                    await centreAssetManagementStub1.MoveAssetToMainAddress.SendWithExceptionAsync(new AssetMoveDto
                    {
                        AddressCategoryHash =
                            await CentreAssetManagementStub.GetCategoryHash.CallAsync(
                                new StringValue {Value = "token_lock"}),
                        HolderId = HolderId,
                        UserToken = "user1",
                        Amount = 1_00000000
                    });
                moveFromUserExchangeDepositAddress1ToMainAddressResult.TransactionResult.Error.ShouldContain(
                    "Sender is not registered as management address in the holder.");
            }

            {
                var moveFromUserExchangeDepositAddress1ToMainAddressResult =
                    await CentreAssetManagementStub.MoveAssetToMainAddress.SendAsync(new AssetMoveDto
                    {
                        AddressCategoryHash =
                            await CentreAssetManagementStub.GetCategoryHash.CallAsync(
                                new StringValue {Value = "token_lock"}),
                        HolderId = HolderId,
                        UserToken = "user1",
                        Amount = 1_00000000
                    });
                var logEvent =
                    moveFromUserExchangeDepositAddress1ToMainAddressResult.TransactionResult.Logs.FirstOrDefault(l =>
                        l.Name == nameof(AssetMovedToMainAddress));


                var assetMovedFromMainAddress = AssetMovedToMainAddress.Parser.ParseFrom(logEvent.NonIndexed);
                assetMovedFromMainAddress.Amount.ShouldBe(1_00000000);
                assetMovedFromMainAddress.From.ShouldBe(userChargingAddress1);

                var assetMovedFromMainAddressIndexed = AssetMovedFromMainAddress.Parser.ParseFrom(logEvent.Indexed[0]);
                assetMovedFromMainAddressIndexed.HolderId.ShouldBe(HolderId);

                var amountInChargingAddress = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
                {
                    Symbol = "ELF",
                    Owner = userChargingAddress1
                });

                amountInChargingAddress.Balance.ShouldBe(9_00000000);

                var holder = await CentreAssetManagementStub.GetHolderInfo.CallAsync(HolderId);
                var holderMainAddress = holder.MainAddress;

                var amountInMainAddress = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
                {
                    Symbol = "ELF",
                    Owner = holderMainAddress
                });

                amountInMainAddress.Balance.ShouldBe(1_00000000);
            }
        }

        [Fact]
        public async Task MoveAssetFromMainAddressTest()
        {
            InitializeContracts();
            var userChargingAddress1 = await GetUserChargingTestAsync();
            await RechargeAsync(10000_00000000, userChargingAddress1);

            await CentreAssetManagementStub.MoveAssetToMainAddress.SendAsync(new AssetMoveDto
            {
                AddressCategoryHash =
                    await CentreAssetManagementStub.GetCategoryHash.CallAsync(
                        new StringValue {Value = "token_lock"}),
                HolderId = HolderId,
                UserToken = "user1",
                Amount = 10000_00000000
            });


            AssetMoveDto assetMoveFromMainToVirtualTokenLockDto = new AssetMoveDto()
            {
                Amount = 5000_00000000,
                UserToken = "user1",
                HolderId = HolderId,
                AddressCategoryHash = HashHelper.ComputeFrom("token_lock")
            };

            {
                var centreAssetManagementStub = GetCentreAssetManagementStub(SampleAccount.Accounts[1].KeyPair);
                var moveFromMainToVirtualTokenLockResult =
                    await centreAssetManagementStub.MoveAssetFromMainAddress.SendWithExceptionAsync(
                        assetMoveFromMainToVirtualTokenLockDto);
                moveFromMainToVirtualTokenLockResult.TransactionResult.Error.ShouldContain(
                    "Current management address can not move this asset.");
            }


            {
                var centreAssetManagementStub = GetCentreAssetManagementStub(SampleAccount.Accounts[5].KeyPair);
                var moveFromMainToVirtualTokenLockResult =
                    await centreAssetManagementStub.MoveAssetFromMainAddress.SendWithExceptionAsync(
                        assetMoveFromMainToVirtualTokenLockDto);
                moveFromMainToVirtualTokenLockResult.TransactionResult.Error.ShouldContain(
                    "Sender is not registered as management address in the holder.");
            }

            {
                var moveAssetFromMainAddress = await CentreAssetManagementStub.MoveAssetFromMainAddress.SendAsync(
                    assetMoveFromMainToVirtualTokenLockDto);
                var logEvent =
                    moveAssetFromMainAddress.TransactionResult.Logs.FirstOrDefault(l =>
                        l.Name == nameof(AssetMovedFromMainAddress));

                var assetMovedFromMainAddress = AssetMovedFromMainAddress.Parser.ParseFrom(logEvent.NonIndexed);
                assetMovedFromMainAddress.Amount.ShouldBe(5000_00000000);
                assetMovedFromMainAddress.To.ShouldBe(userChargingAddress1);

                var assetMovedFromMainAddressIndexed = AssetMovedFromMainAddress.Parser.ParseFrom(logEvent.Indexed[0]);
                assetMovedFromMainAddressIndexed.HolderId.ShouldBe(HolderId);
            }
        }

        [Fact]
        public async Task RequestWithdrawTest()
        {
            InitializeContracts();
            var userChargingAddress1 = await GetUserChargingTestAsync();
            await RechargeAsync(10000_00000000, userChargingAddress1);

            {
                var result = await CentreAssetManagementStub.RequestWithdraw.SendWithExceptionAsync(
                    new WithdrawRequestDto
                    {
                        HolderId = HolderId,
                        Amount = 10000_0000000
                    });
                result.TransactionResult.Error.ShouldContain("Address required.");
            }

            {
                var result = await CentreAssetManagementStub.RequestWithdraw.SendWithExceptionAsync(
                    new WithdrawRequestDto
                    {
                        HolderId = HolderId,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                    });
                result.TransactionResult.Error.ShouldContain("Amount required.");
            }

            {
                var result = await CentreAssetManagementStub.RequestWithdraw.SendWithExceptionAsync(
                    new WithdrawRequestDto
                    {
                        HolderId = HashHelper.ComputeFrom("HolderId"),
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                        Amount = 10000_0000000
                    });
                result.TransactionResult.Error.ShouldContain("Holder is not initialized.");
            }
            
            {
                var stub = GetCentreAssetManagementStub(SampleAccount.Accounts[1].KeyPair);
                var result = await stub.RequestWithdraw.SendWithExceptionAsync(
                    new WithdrawRequestDto
                    {
                        HolderId = HolderId,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                        Amount = 10000_0000000
                    });
                result.TransactionResult.Error.ShouldContain("Current management address can not move this asset.");
            }

            {
                var stub = GetCentreAssetManagementStub(SampleAccount.Accounts[2].KeyPair);
                var result = await stub.RequestWithdraw.SendWithExceptionAsync(
                    new WithdrawRequestDto
                    {
                        HolderId = HolderId,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                        Amount = 10000_0000000
                    });
                result.TransactionResult.Error.ShouldContain("Current key cannot make withdraw request.");
            }
            
            {
                var result = await CentreAssetManagementStub.RequestWithdraw.SendWithExceptionAsync(
                    new WithdrawRequestDto
                    {
                        HolderId = HolderId,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                        Amount = 10000_0000000
                    });
                result.TransactionResult.Error.ShouldContain("Insufficient balance to withdraw.");
            }
            
            // move to main address
            await CentreAssetManagementStub.MoveAssetToMainAddress.SendAsync(new AssetMoveDto
            {
                AddressCategoryHash =
                    await CentreAssetManagementStub.GetCategoryHash.CallAsync(
                        new StringValue {Value = "token_lock"}),
                HolderId = HolderId,
                UserToken = "user1",
                Amount = 10000_00000000
            });
            
            {
                var result = await CentreAssetManagementStub.RequestWithdraw.SendAsync(
                    new WithdrawRequestDto
                    {
                        HolderId = HolderId,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                        Amount = 10000_0000000
                    });

                var logEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == nameof(WithdrawRequested));
                logEvent.ShouldNotBeNull();
                var withdrawRequested = WithdrawRequested.Parser.ParseFrom(logEvent.NonIndexed);
                withdrawRequested.Amount.ShouldBe(10000_0000000);
                withdrawRequested.RequestAddress.ShouldBe(Address.FromPublicKey(DefaultKeyPair.PublicKey));
                withdrawRequested.WithdrawId.ShouldNotBeNull();
                withdrawRequested.WithdrawAddress.ShouldBe(Address.FromPublicKey(DefaultKeyPair.PublicKey));
                
                var withdrawRequestedIndexed = WithdrawRequested.Parser.ParseFrom(logEvent.Indexed.First());
                withdrawRequestedIndexed.HolderId.ShouldBe(HolderId);

                var withdrawInfo =
                    await CentreAssetManagementStub.GetPendingWithdraw.CallAsync(withdrawRequested.WithdrawId);
                withdrawInfo.Amount.ShouldBe(withdrawRequested.Amount);
                withdrawInfo.Address.ShouldBe(withdrawRequested.WithdrawAddress);
                withdrawInfo.HolderId.ShouldBe(withdrawRequestedIndexed.HolderId);
                withdrawInfo.TotalRequired.ShouldBe(2);
                // withdrawInfo.AddedTime
                withdrawInfo.ManagementAddressesLimitAmount.ShouldBe(1000_000_00000000);
                withdrawInfo.ApprovedAddresses.Count.ShouldBe(1);
                withdrawInfo.ApprovedAddresses.ShouldContain(Address.FromPublicKey(DefaultKeyPair.PublicKey));
            }
        }

        [Fact]
        public async Task ApproveWithdrawTest()
        {
            InitializeContracts();
            var userChargingAddress1 = await GetUserChargingTestAsync();
            await RechargeAsync(10000_00000000, userChargingAddress1);
            
            // move to main address
            await CentreAssetManagementStub.MoveAssetToMainAddress.SendAsync(new AssetMoveDto
            {
                AddressCategoryHash =
                    await CentreAssetManagementStub.GetCategoryHash.CallAsync(
                        new StringValue {Value = "token_lock"}),
                HolderId = HolderId,
                UserToken = "user1",
                Amount = 10000_00000000
            });
            
            var requestResult = await CentreAssetManagementStub.RequestWithdraw.SendAsync(
                new WithdrawRequestDto
                {
                    HolderId = HolderId,
                    Address = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                    Amount = 10000_0000000,
                });
            
            var requestLogEvent = requestResult.TransactionResult.Logs.FirstOrDefault(l => l.Name == nameof(WithdrawRequested));
            var withdrawRequested = WithdrawRequested.Parser.ParseFrom(requestLogEvent.NonIndexed);

            // request
            await CentreAssetManagementStub.RequestWithdraw.SendAsync(
                new WithdrawRequestDto
                {
                    HolderId = HolderId,
                    Address = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                    Amount = 10000_0000000
                });

            {
                var result = await CentreAssetManagementStub.ApproveWithdraw.SendWithExceptionAsync(
                    new WithdrawApproveDto
                    {
                        Id = HashHelper.ComputeFrom("WithdrawId"),
                        Amount = 10000_0000000,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey)
                    });
                result.TransactionResult.Error.ShouldContain("Withdraw not exists.");
            }
            
            {
                var result = await CentreAssetManagementStub.ApproveWithdraw.SendWithExceptionAsync(
                    new WithdrawApproveDto
                    {
                        Id = withdrawRequested.WithdrawId,
                        Amount = 10000_000000,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey)
                    });
                result.TransactionResult.Error.ShouldContain("Withdraw data not matched.");
            }
            
            {
                var result = await CentreAssetManagementStub.ApproveWithdraw.SendWithExceptionAsync(
                    new WithdrawApproveDto
                    {
                        Id = withdrawRequested.WithdrawId,
                        Amount = 10000_0000000,
                        Address = Accounts[1].Address
                    });
                result.TransactionResult.Error.ShouldContain("Withdraw data not matched.");
            }

            {
                var stub = GetCentreAssetManagementStub(Accounts[5].KeyPair);
                var result = await stub.ApproveWithdraw.SendWithExceptionAsync(
                    new WithdrawApproveDto
                    {
                        Id = withdrawRequested.WithdrawId,
                        Amount = 10000_0000000,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey)
                    });
                result.TransactionResult.Error.ShouldContain(
                    "Sender is not registered as management address in the holder.");
            }
            
            {
                var stub = GetCentreAssetManagementStub(Accounts[5].KeyPair);
                var result = await stub.ApproveWithdraw.SendWithExceptionAsync(
                    new WithdrawApproveDto
                    {
                        Id = withdrawRequested.WithdrawId,
                        Amount = 10000_0000000,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey)
                    });
                result.TransactionResult.Error.ShouldContain(
                    "Sender is not registered as management address in the holder.");
            }
            
            {
                var stub = GetCentreAssetManagementStub(Accounts[1].KeyPair);
                var result = await stub.ApproveWithdraw.SendWithExceptionAsync(
                    new WithdrawApproveDto
                    {
                        Id = withdrawRequested.WithdrawId,
                        Amount = 10000_0000000,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey)
                    });
                result.TransactionResult.Error.ShouldContain(
                    "Current management address cannot approve, amount limited.");
            }
            
            {
                var result = await CentreAssetManagementStub.ApproveWithdraw.SendAsync(
                    new WithdrawApproveDto
                    {
                        Id = withdrawRequested.WithdrawId,
                        Amount = 10000_0000000,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey)
                    });
                var logEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == nameof(WithdrawReleased));
                logEvent.ShouldBeNull();
            }

            var holder = await CentreAssetManagementStub.GetHolderInfo.CallAsync(HolderId);
            await CheckBalanceAsync(holder.MainAddress, 10000_00000000);
            {
                var stub = GetCentreAssetManagementStub(Accounts[2].KeyPair);
                var result = await stub.ApproveWithdraw.SendAsync(
                    new WithdrawApproveDto
                    {
                        Id = withdrawRequested.WithdrawId,
                        Amount = 10000_0000000,
                        Address = Address.FromPublicKey(DefaultKeyPair.PublicKey)
                    });
                var logEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == nameof(WithdrawReleased));
                logEvent.ShouldNotBeNull();
                var withdrawReleased = WithdrawReleased.Parser.ParseFrom(logEvent.NonIndexed);
                withdrawReleased.Amount.ShouldBe(10000_0000000);
                withdrawReleased.WithdrawAddress.ShouldBe(Address.FromPublicKey(DefaultKeyPair.PublicKey));

                {
                    var withdrawReleasedIndexed = WithdrawReleased.Parser.ParseFrom(logEvent.Indexed.First());
                    withdrawReleasedIndexed.HolderId.ShouldBe(HolderId);
                }
                
                {
                    var withdrawReleasedIndexed = WithdrawReleased.Parser.ParseFrom(logEvent.Indexed.Last());
                    withdrawReleasedIndexed.WithdrawId.ShouldBe(withdrawRequested.WithdrawId);
                }
            }
            
            var withdrawInfo =
                await CentreAssetManagementStub.GetPendingWithdraw.CallAsync(withdrawRequested.WithdrawId);
            withdrawInfo.ShouldBe(new WithdrawInfo());
            await CheckBalanceAsync(holder.MainAddress, 9000_00000000);
        }

        [Fact]
        public async Task CancelWithdrawsTest()
        {
            InitializeContracts();
            var userChargingAddress1 = await GetUserChargingTestAsync();
            await RechargeAsync(10000_00000000, userChargingAddress1);
            
            // move to main address
            await CentreAssetManagementStub.MoveAssetToMainAddress.SendAsync(new AssetMoveDto
            {
                AddressCategoryHash =
                    await CentreAssetManagementStub.GetCategoryHash.CallAsync(
                        new StringValue {Value = "token_lock"}),
                HolderId = HolderId,
                UserToken = "user1",
                Amount = 10000_00000000
            });
            
            var requestResult = await CentreAssetManagementStub.RequestWithdraw.SendAsync(
                new WithdrawRequestDto
                {
                    HolderId = HolderId,
                    Address = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                    Amount = 10000_0000000,
                });

            var requestLogEvent = requestResult.TransactionResult.Logs.FirstOrDefault(l => l.Name == nameof(WithdrawRequested));
            var withdrawRequested = WithdrawRequested.Parser.ParseFrom(requestLogEvent.NonIndexed);
            var withdrawId = withdrawRequested.WithdrawId;
            // var withdrawInfo = await CentreAssetManagementStub.GetPendingWithdraw.CallAsync(withdrawId);

            {
                var cancelWithdrawResult = await CentreAssetManagementStub.CancelWithdraws.SendWithExceptionAsync(
                    new CancelWithdrawsDto
                    {
                        HolderId = HashHelper.ComputeFrom("HolderId")
                    });
                
                cancelWithdrawResult.TransactionResult.Error.ShouldContain("Holder is not initialized.");
            }

            {
                //withdraw id not exists
                await CentreAssetManagementStub.CancelWithdraws.SendAsync(
                    new CancelWithdrawsDto
                    {
                        HolderId = HolderId,
                        Ids = {HashHelper.ComputeFrom("WithdrawId")}
                    });
            }
            
            {
                //withdraw id not exists
                await CentreAssetManagementStub.CancelWithdraws.SendAsync(
                    new CancelWithdrawsDto
                    {
                        HolderId = HolderId,
                        Ids = {HashHelper.ComputeFrom("WithdrawId")}
                    });
            }

            {
                // create new holder
                var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendAsync(new HolderCreateDto()
                {
                    Symbol = "ELF",
                    ManagementAddresses =
                    {
                        new ManagementAddress()
                        {
                            Address = Address.FromPublicKey(SampleAccount.Accounts[0].KeyPair.PublicKey),
                            Amount = long.MaxValue,
                            ManagementAddressesLimitAmount = 1000_000_00000000,
                            ManagementAddressesInTotal = 2
                        },
                        new ManagementAddress()
                        {
                            Address = Address.FromPublicKey(SampleAccount.Accounts[1].KeyPair.PublicKey),
                            Amount = 1000_000000,
                            ManagementAddressesLimitAmount = 1000_000,
                        },
                        new ManagementAddress()
                        {
                            Address = Address.FromPublicKey(SampleAccount.Accounts[2].KeyPair.PublicKey),
                            Amount = 1000_000_00000000,
                            ManagementAddressesLimitAmount = 1000_000_00000000,
                        }
                    },
                    OwnerAddress = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                    ShutdownAddress = Address.FromPublicKey(DefaultKeyPair.PublicKey),
                    SettingsEffectiveTime = 3600
                });

                {
                    var cancelWithdrawResult = await CentreAssetManagementStub.CancelWithdraws.SendWithExceptionAsync(
                        new CancelWithdrawsDto
                        {
                            HolderId = createHolderResult.Output.Id,
                            Ids = {withdrawId}
                        });
                    cancelWithdrawResult.TransactionResult.Error.ShouldContain("Holder not matched.");
                }
            }
            
            var withdrawInfoBefore = await CentreAssetManagementStub.GetPendingWithdraw.CallAsync(withdrawId);
            withdrawInfoBefore.ShouldNotBeNull();
            
            await CentreAssetManagementStub.CancelWithdraws.SendAsync(
                new CancelWithdrawsDto
                {
                    HolderId = HolderId,
                    Ids = {withdrawId}
                });

            var withdrawInfoAfter = await CentreAssetManagementStub.GetPendingWithdraw.CallAsync(withdrawId);
            withdrawInfoAfter.ShouldBe(new WithdrawInfo());
        }

        [Fact]
        public async Task MainTest()
        {
            InitializeContracts();

            //origin address
            //deposit address (exchange virtual user address)
            //vote address (exchange virtual user address)
            //main address (exchange virtual main address)

            AssetMoveDto assetMoveFromVirtualToMainDto1 = new AssetMoveDto()
            {
                Amount = 10_00000000,
                UserToken = "UserToken1",
                HolderId = HolderId
            };

            var userExchangeDepositAddress1 =
                await CentreAssetManagementStub.GetVirtualAddress.CallAsync(new VirtualAddressCalculationDto
                {
                    UserToken = "UserToken1",
                    HolderId = HolderId
                });

            await TokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                Amount = 10_00000000,
                Symbol = "ELF",
                To = userExchangeDepositAddress1
            });

            await CheckBalanceAsync(userExchangeDepositAddress1, 10_00000000);


            //move elf token to main address
            var moveFromUserExchangeDepositAddress1ToMainAddressResult =
                await CentreAssetManagementStub.MoveAssetToMainAddress.SendAsync(assetMoveFromVirtualToMainDto1);

            Assert.True(moveFromUserExchangeDepositAddress1ToMainAddressResult.Output.Success);

            AssetMoveDto assetMoveFromMainToVirtualTokenLockDto1 = new AssetMoveDto()
            {
                Amount = 5_00000000,
                UserToken = "UserToken1",
                HolderId = HolderId,
                AddressCategoryHash = HashHelper.ComputeFrom("token_lock")
            };

            //move elf token from main address to user1's voting address
            var moveFromMainToVirtualTokenLockResult =
                await CentreAssetManagementStub.MoveAssetFromMainAddress.SendAsync(
                    assetMoveFromMainToVirtualTokenLockDto1);

            Assert.True(moveFromMainToVirtualTokenLockResult.Output.Success);


            var userVoteAddress1 =
                await CentreAssetManagementStub.GetVirtualAddress.CallAsync(new VirtualAddressCalculationDto
                {
                    UserToken = "UserToken1",
                    HolderId = HolderId,
                    AddressCategoryHash = HashHelper.ComputeFrom("token_lock")
                });

            await CheckBalanceAsync(userVoteAddress1, 5_00000000);

            {
                await CentreAssetManagementStub.SendTransactionByUserVirtualAddress.SendAsync(
                    new SendTransactionByUserVirtualAddressDto()
                    {
                        Args = new TransferInput()
                        {
                            Amount = 1_00000000,
                            Symbol = "ELF",
                            To = userExchangeDepositAddress1
                        }.ToByteString(),
                        MethodName = "Transfer",
                        To = TokenContractAddress,
                        HolderId = HolderId,
                        UserToken = "UserToken1",
                        AddressCategoryHash = HashHelper.ComputeFrom("token_lock"),
                    });
            }

            await CheckBalanceAsync(userVoteAddress1, 4_00000000);


            var withdrawAddress1 = Address.FromPublicKey(SampleAccount.Accounts[4].KeyPair.PublicKey);

            //withdraw to user1 origin address
            var requestWithdrawToOriginAddress1Result = await CentreAssetManagementStub.RequestWithdraw.SendAsync(
                new WithdrawRequestDto()
                {
                    Address = withdrawAddress1,
                    Amount = 3_00000000,
                    HolderId = HolderId
                });

            var withdrawRequest1 = requestWithdrawToOriginAddress1Result.Output.Id;

            Assert.True(withdrawRequest1 != null);

            var centreAssetManagementStub2 = GetCentreAssetManagementStub(SampleAccount.Accounts[1].KeyPair);

            var manageAddress2ApproveWithdraw = await centreAssetManagementStub2.ApproveWithdraw.SendWithExceptionAsync(
                new WithdrawApproveDto()
                {
                    Address = withdrawAddress1, //must keep the same with original withdraw
                    Amount = 3_00000000, //must keep the same with original withdraw,
                    Id = withdrawRequest1
                });

            manageAddress2ApproveWithdraw.TransactionResult.Error.Contains(
                "current management address cannot approve, amount limited");

            var centreAssetManagementStub3 = GetCentreAssetManagementStub(SampleAccount.Accounts[2].KeyPair);

            var manageAddress3ApproveWithdraw = await centreAssetManagementStub3.ApproveWithdraw.SendAsync(
                new WithdrawApproveDto()
                {
                    Address = withdrawAddress1, //must keep the same with original withdraw
                    Amount = 3_00000000, //must keep the same with original withdraw,
                    Id = withdrawRequest1
                });

            await CheckBalanceAsync(withdrawAddress1, 3_00000000);
        }

        private async Task CheckBalanceAsync(Address userVoteAddress1, long expect)
        {
            var useVoteAddressBalance1 = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = userVoteAddress1,
                Symbol = "ELF"
            });

            Assert.Equal(expect, useVoteAddressBalance1.Balance);
        }


        private Address GetAddress(int index)
        {
            return Address.FromPublicKey(SampleAccount.Accounts[index].KeyPair.PublicKey);
        }

        [Fact]
        public async Task HackAttackAndShutdown()
        {
            InitializeContracts();
            var addressOwner = GetAddress(1);
            var shutdownAddress = GetAddress(2);

            var createHolderResult = await CentreAssetManagementStub.CreateHolder.SendAsync(new HolderCreateDto()
            {
                Symbol = "ELF",
                OwnerAddress = addressOwner,
                SettingsEffectiveTime = 5,
                ShutdownAddress = shutdownAddress,
            });

            var holderId = createHolderResult.Output.Id;

            //use OwnerAddress to shutdown
            await GetCentreAssetManagementStub(SampleAccount.Accounts[1].KeyPair).ShutdownHolder
                .SendAsync(new HolderShutdownDto() {HolderId = holderId});

            await CentreAssetManagementStub.RebootHolder.SendAsync(new HolderRebootDto()
                {HolderOwner = addressOwner, HolderId = holderId});

            //use ShutdownAddress to shutdown
            await GetCentreAssetManagementStub(SampleAccount.Accounts[2].KeyPair).ShutdownHolder
                .SendAsync(new HolderShutdownDto {HolderId = holderId});

            await CentreAssetManagementStub.RebootHolder.SendAsync(new HolderRebootDto()
                {HolderOwner = addressOwner, HolderId = holderId});
        }

        private async Task<Address> GetUserChargingTestAsync()
        {
            return await CentreAssetManagementStub.GetVirtualAddress.CallAsync(
                new VirtualAddressCalculationDto
                {
                    AddressCategoryHash =
                        await CentreAssetManagementStub.GetCategoryHash.CallAsync(
                            new StringValue {Value = "token_lock"}),
                    HolderId = HolderId,
                    UserToken = "user1"
                });
        }

        private async Task RechargeAsync(long amount, Address userChargingAddress)
        {
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Amount = amount,
                Symbol = "ELF",
                To = userChargingAddress
            });
        }
    }
}