using Microsoft.Extensions.DependencyInjection;
using Otus.Teaching.PromoCodeFactory.WebHost.Services;

namespace Otus.Teaching.PromoCodeFactory.WebHost.Tools
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPatnerService(this IServiceCollection services)
        {
            services.AddSingleton<IPartnerService, PartnerService>();
            return services;
        }
    }
}