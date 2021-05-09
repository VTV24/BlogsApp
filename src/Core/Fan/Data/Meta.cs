using System.ComponentModel.DataAnnotations;

namespace Fan.Data
{
    /// <summary>
    /// A key value pair record.
    /// </summary>
    /// <remarks>
    /// The Meta table stores settings and other json strings. It has a unique constrain on Type and Key pair.
    /// </remarks>
    public class Meta : Entity
    {
        [Required]
        [StringLength(maximumLength: 256)]
        public string Key { get; set; }

        public string Value { get; set; }

        public EMetaType Type { get; set; }
    }
}