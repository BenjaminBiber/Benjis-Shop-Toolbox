using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DBCloner.Models;

public partial class CustomWidgetLocation
{
    [Key]
    [StringLength(100)]
    public string Label { get; set; } = null!;

    [StringLength(500)]
    public string Description { get; set; } = null!;

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public byte[] Rowversion { get; set; } = null!;
}
