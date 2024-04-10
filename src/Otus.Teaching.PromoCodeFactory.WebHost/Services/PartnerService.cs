using Otus.Teaching.PromoCodeFactory.Core.Abstractions.Repositories;
using Otus.Teaching.PromoCodeFactory.Core.Domain.PromoCodeManagement;
using System.Threading.Tasks;
using System;
using Otus.Teaching.PromoCodeFactory.WebHost.Models;
using System.Linq;

namespace Otus.Teaching.PromoCodeFactory.WebHost.Services
{
    public class PartnerService : IPartnerService
    {
        private readonly IRepository<Partner> _partnersRepository;

        public PartnerService(IRepository<Partner> partnersRepository)
        {
            _partnersRepository = partnersRepository;
        }

        public async Task<OperationResult<PartnerPromoCodeLimit>> SetPromoCodeLimitAsync(Guid partnerId, int limit, DateTime endDate)
        {
            var partner = await _partnersRepository.GetByIdAsync(partnerId);

            if (partner == null)
                return OperationResult<PartnerPromoCodeLimit>.NotFound("Партнер не найден.");

            if (!partner.IsActive)
                return OperationResult<PartnerPromoCodeLimit>.BadRequest("Партнер не активен.");

            if (limit <= 0)
                return OperationResult<PartnerPromoCodeLimit>.BadRequest("Лимит должен быть больше 0.");


            if (partner.PartnerLimits.FirstOrDefault(x => !x.CancelDate.HasValue) is { } activeLimit) 
            {
                partner.NumberIssuedPromoCodes = 0;
                activeLimit.CancelDate = DateTime.UtcNow;
            }

            var newLimit = new PartnerPromoCodeLimit()
            {
                Limit = limit,
                PartnerId = partner.Id,
                CreateDate = DateTime.UtcNow,
                EndDate = endDate
            };

            partner.PartnerLimits.Add(newLimit);
            await _partnersRepository.UpdateAsync(partner);

            return OperationResult<PartnerPromoCodeLimit>.Ok(newLimit);
        }
    }
}
