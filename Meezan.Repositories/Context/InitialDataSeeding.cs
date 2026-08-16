using Common.Enums;
using Common.ExtensionMethods;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meezan.Repositories.Context
{
    public static class InitialDataSeeding
    {
        public static void SeedInitialData(this ModelBuilder modelBuilder)
        {
            #region MailStatus
            modelBuilder.Entity<MailStatus>().HasData(
                        new MailStatus
                        {
                            MailStatusId = (int)MailStatusEnum.Draft,
                            ArName = "مسودة",
                            EnName = "Draft",
                            ArDescription = "مسودة",
                            EnDescription = "Draft",
                            CreationTime = new DateTime(2023, 01, 1)
                        },
                        new MailStatus
                        {
                            MailStatusId = (int)MailStatusEnum.Sent,
                            ArName = "تم الإرسال",
                            EnName = "Sent",
                            ArDescription = "تم الإرسال",
                            EnDescription = "Sent",
                            CreationTime = new DateTime(2023, 01, 1)
                        },
                        new MailStatus
                        {
                            MailStatusId = 3,
                            ArName = "معالجة",
                            EnName = "Processing",
                            ArDescription = "معالجة",
                            EnDescription = "Processing",
                            CreationTime = new DateTime(2023, 01, 1)
                        },
                        new MailStatus
                        {
                            MailStatusId = (int)MailStatusEnum.Failed,
                            ArName = "فشل",
                            EnName = "Failed",
                            ArDescription = "فشل",
                            EnDescription = "Failed",
                            CreationTime = new DateTime(2023, 01, 1)
                        });
            #endregion

            #region MailType

            modelBuilder.Entity<MailType>().HasData(
                       new MailType
                       {
                           MailTypeId = (int)MailTypeEnum.ForgetPassword,
                           ArName = "نسيان كلمة السر",
                           EnName = "Forget Password",
                           ArDescription = "نسيان كلمة السر",
                           EnDescription = "Forget Password",
                           CreationTime = new DateTime(2023, 01, 1)
                       },
                       new MailType
                       {
                           MailTypeId = (int)MailTypeEnum.WelcomeMail,
                           ArName = "بريد الترحيب",
                           EnName = "Welcome Mail",
                           ArDescription = "بريد الترحيب",
                           EnDescription = "Welcome Mail",
                           CreationTime = new DateTime(2023, 01, 1)
                       },
                       new MailType
                       {
                           MailTypeId = (int)MailTypeEnum.VerificationMail,
                           ArName = "بريد التحقق",
                           EnName = "Verification Mail",
                           ArDescription = "بريد التحقق",
                           EnDescription = "Verification Mail",
                           CreationTime = new DateTime(2023, 01, 1),
                       }
                       );

            #endregion

            #region Currency

            modelBuilder.Entity<Currency>().HasData(
                       new Currency
                       {
                           Code = "USD",
                           Type = CurrencyType.Fiat,
                           Symbol = "$",
                           Decimals = 2,
                           EnName = "US Dollar",
                           ArName = "دولار أمريكي",
                           EnDescription = "United States Dollar",
                           ArDescription = "دولار الولايات المتحدة الأمريكية",
                           CreationTime = new DateTime(2026, 01, 1)
                       },
                       new Currency
                       {
                           Code = "SAR",
                           Type = CurrencyType.Fiat,
                           Symbol = "SAR",
                           Decimals = 2,
                           EnName = "Saudi Riyal",
                           ArName = "ريال سعودي",
                           EnDescription = "Saudi Arabian Riyal",
                           ArDescription = "الريال السعودي",
                           CreationTime = new DateTime(2026, 01, 1)
                       },
                       new Currency
                       {
                           Code = "EGP",
                           Type = CurrencyType.Fiat,
                           Symbol = "EGP",
                           Decimals = 2,
                           EnName = "Egyptian Pound",
                           ArName = "جنيه مصري",
                           EnDescription = "Egyptian Pound",
                           ArDescription = "الجنيه المصري",
                           CreationTime = new DateTime(2026, 01, 1)
                       },
                       new Currency
                       {
                           Code = "GOLD",
                           Type = CurrencyType.Metal,
                           Symbol = "g",
                           Decimals = 3,
                           EnName = "Gold",
                           ArName = "ذهب",
                           EnDescription = "Gold (grams, pure 24K equivalent for Zakat)",
                           ArDescription = "ذهب (بالجرام، مكافئ عيار 24 للزكاة)",
                           CreationTime = new DateTime(2026, 01, 1)
                       },
                       new Currency
                       {
                           Code = "SILVER",
                           Type = CurrencyType.Metal,
                           Symbol = "g",
                           Decimals = 3,
                           EnName = "Silver",
                           ArName = "فضة",
                           EnDescription = "Silver (grams)",
                           ArDescription = "فضة (بالجرام)",
                           CreationTime = new DateTime(2026, 01, 1)
                       }
                       );

            #endregion

            #region WalletType

            modelBuilder.Entity<WalletType>().HasData(
                       new WalletType
                       {
                           WalletTypeId = 1,
                           EnName = "General",
                           ArName = "عام",
                           EnDescription = "General purpose wallet",
                           ArDescription = "محفظة لأغراض عامة",
                           CreationTime = new DateTime(2026, 01, 1)
                       },
                       new WalletType
                       {
                           WalletTypeId = 2,
                           EnName = "Bank Account",
                           ArName = "حساب بنكي",
                           EnDescription = "Bank account wallet",
                           ArDescription = "محفظة حساب بنكي",
                           CreationTime = new DateTime(2026, 01, 1)
                       },
                       new WalletType
                       {
                           WalletTypeId = 3,
                           EnName = "Cash",
                           ArName = "نقدي",
                           EnDescription = "Cash wallet",
                           ArDescription = "محفظة نقدية",
                           CreationTime = new DateTime(2026, 01, 1)
                       }
                       );

            #endregion

        }
    }
}
