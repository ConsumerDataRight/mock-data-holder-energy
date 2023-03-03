using CDR.DataHolder.Repository.Entities;
using CDR.DataHolder.Repository.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CDR.DataHolder.Repository
{
    public class DataRecipientRepository
    {
        private readonly DbContextOptions<DataHolderDatabaseContext> _options;

        public DataRecipientRepository(string connString)
        {
            _options = new DbContextOptionsBuilder<DataHolderDatabaseContext>().UseSqlServer(connString).Options;
        }

        public async Task<Exception> InsertDataRecipient(LegalEntity regDataRecipient)
        {
            try
            {
                using (var dhDbContext = new DataHolderDatabaseContext(_options))
                {
                    using (var txn = dhDbContext.Database.BeginTransaction())
                    {
                        // Insert LegalEntity entity including its child Brands and SoftwareProducts entities
                        dhDbContext.Add(regDataRecipient);
                        await dhDbContext.SaveChangesAsync();
                        await txn.CommitAsync();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        

        public async Task<Exception> DeleteDataRecipients(IList<LegalEntity> dhDataRecipients)
        {
            try
            {
                using (var dhDbContext = new DataHolderDatabaseContext(_options))
                {
                    using (var txn = dhDbContext.Database.BeginTransaction())
                    {
                        dhDbContext.RemoveRange(dhDataRecipients);
                        await dhDbContext.SaveChangesAsync();
                        await txn.CommitAsync();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public async Task<Exception> InsertBrand(Brand regDrBrand)
        {
            try
            {
                using (var dhDbContext = new DataHolderDatabaseContext(_options))
                {
                    using (var txn = dhDbContext.Database.BeginTransaction())
                    {
                        // Insert the Register Brand and any SoftwareProduct entities
                        IList<SoftwareProduct> swProducts = new List<SoftwareProduct>();
                        foreach (var swProdItem in regDrBrand.SoftwareProducts)
                        {
                            swProducts.Add(new()
                            {
                                SoftwareProductId = swProdItem.SoftwareProductId,
                                SoftwareProductName = swProdItem.SoftwareProductName,
                                SoftwareProductDescription = swProdItem.SoftwareProductDescription,
                                LogoUri = swProdItem.LogoUri,
                                Status = swProdItem.Status
                            });
                        }
                        Brand brand = new()
                        {
                            BrandId = regDrBrand.BrandId,
                            BrandName = regDrBrand.BrandName,
                            LogoUri = regDrBrand.LogoUri,
                            Status = regDrBrand.Status,
                            SoftwareProducts = swProducts,
                            LegalEntityId = regDrBrand.LegalEntityId
                        };

                        dhDbContext.Add(brand);
                        await dhDbContext.SaveChangesAsync();
                        await txn.CommitAsync();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public async Task<Exception> DeleteBrands(IList<Brand> dhBrands)
        {
            try
            {
                using (var dhDbContext = new DataHolderDatabaseContext(_options))
                {
                    using (var txn = dhDbContext.Database.BeginTransaction())
                    {
                        dhDbContext.RemoveRange(dhBrands);
                        await dhDbContext.SaveChangesAsync();
                        await txn.CommitAsync();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public async Task<Exception> InsertSoftwareProduct(SoftwareProduct regDrSwProd)
        {
            try
            {
                using (var dhDbContext = new DataHolderDatabaseContext(_options))
                {
                    using (var txn = dhDbContext.Database.BeginTransaction())
                    {
                        // Insert the Register SoftwareProduct entity
                        SoftwareProduct swProduct = new()
                        {
                            SoftwareProductId = regDrSwProd.SoftwareProductId,
                            SoftwareProductName = regDrSwProd.SoftwareProductName,
                            SoftwareProductDescription = regDrSwProd.SoftwareProductDescription,
                            LogoUri = regDrSwProd.LogoUri,
                            Status = regDrSwProd.Status,
                            BrandId = regDrSwProd.BrandId
                        };

                        dhDbContext.Add(swProduct);
                        await dhDbContext.SaveChangesAsync();
                        await txn.CommitAsync();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public async Task<Exception> DeleteSoftwareProduct(IList<SoftwareProduct> dhSwProds)
        {
            try
            {
                using (var dhDbContext = new DataHolderDatabaseContext(_options))
                {
                    using (var txn = dhDbContext.Database.BeginTransaction())
                    {
                        dhDbContext.RemoveRange(dhSwProds);
                        await dhDbContext.SaveChangesAsync();
                        await txn.CommitAsync();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}