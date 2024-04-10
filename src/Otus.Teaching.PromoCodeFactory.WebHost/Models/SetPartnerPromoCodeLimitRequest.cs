using System;
using System.ComponentModel.DataAnnotations;
using Otus.Teaching.PromoCodeFactory.Core.Domain.PromoCodeManagement;

namespace Otus.Teaching.PromoCodeFactory.WebHost.Models
{
    public class SetPartnerPromoCodeLimitRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Лимит должен быть больше 0.")]
        public int Limit { get; set; }
        public DateTime EndDate { get; set; }
    }
}