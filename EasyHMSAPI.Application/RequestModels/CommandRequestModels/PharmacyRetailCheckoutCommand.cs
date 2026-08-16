using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class PharmacyRetailCheckoutCommand : IRequest<PharmacyRetailCheckoutResponseModel>
    {
        [Required]
        public Guid HospitalId { get; set; }
        
        [Required]
        public Guid StoreId { get; set; }

        public string? PatientId { get; set; } // If null, it's an anonymous walk-in
        public string? WalkInName { get; set; }
        public string? WalkInContact { get; set; }
        public Guid? PrescribingDoctorId { get; set; }

        [Required]
        public List<PharmacyCartItem> Items { get; set; } = new();

        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal PaidAmount { get; set; } // Allows partial/credit payments
        public string? PaymentMode { get; set; }

        public string? LoggedInUserName { get; set; }
        public Guid? LoggedInUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PharmacyCartItem
    {
        public Guid InventoryItemId { get; set; }
        public Guid? BatchId { get; set; }
        public decimal Qty { get; set; }
        public decimal Rate { get; set; } // The rate applied at checkout
        public decimal DiscountPercent { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PharmacyRetailCheckoutResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid EncounterId { get; set; }
        public Guid ChargeEventId { get; set; }
        public Guid InvoiceId { get; set; }
        public string? InvoiceNo { get; set; }
    }
}
