using Otus.Teaching.PromoCodeFactory.Core.Domain.PromoCodeManagement;
using Otus.Teaching.PromoCodeFactory.WebHost.Models;
using System;
using System.Threading.Tasks;

namespace Otus.Teaching.PromoCodeFactory.WebHost.Services
{
    public interface IPartnerService
    {
        Task<OperationResult<PartnerPromoCodeLimit>> SetPromoCodeLimitAsync(Guid partnerId, int limit, DateTime endDate);
    }
}