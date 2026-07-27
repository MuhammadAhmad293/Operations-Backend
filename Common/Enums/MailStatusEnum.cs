using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Common.Enums
{
    public enum MailStatusEnum
    {

        [Display(Name = "مسودة", ShortName = "Draft")]
        [Description("Draft")]
        Draft = 1,

        [Display(Name = "تم الإرسال", ShortName = "Sent")]
        [Description("Sent")]
        Sent = 2,

        [Display(Name = "فشل", ShortName = "Failed")]
        [Description("Failed")]
        Failed = 4
    }
}
