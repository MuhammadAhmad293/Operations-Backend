using Common.Enums;
using Common.ExtensionMethods;
using Operations.DataModel.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Operations.Repositories.Context
{
    public static class InitialDataSeeding
    {
        public static void SeedInitialData(this ModelBuilder modelBuilder)
        {
            #region MailStatus
            modelBuilder.Entity<MailStatus>().HasData(
                        new MailStatus
                        {
                            MailStatusId = (int)MailStatusEnum.New,
                            ArName = "جديد",
                            EnName = "New",
                            ArDescription = "جديد",
                            EnDescription = "New",
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
                            MailStatusId = (int)MailStatusEnum.Processing,
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

        }
    }
}
