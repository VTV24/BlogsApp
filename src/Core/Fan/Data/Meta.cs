using System.ComponentModel.DataAnnotations;

namespace Fan.Data
{
    public class Meta : Entity
    {
        [Required]
        [StringLength(maximumLength: 256)]
        public string Key { get; set; }
        public string Value { get; set; }

        public EMetaType Type { get; set; }
    }
}