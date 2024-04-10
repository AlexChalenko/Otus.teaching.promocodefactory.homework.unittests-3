using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Namotion.Reflection;
using Otus.Teaching.PromoCodeFactory.Core.Abstractions.Repositories;
using Otus.Teaching.PromoCodeFactory.Core.Domain.PromoCodeManagement;
using Otus.Teaching.PromoCodeFactory.WebHost.Controllers;
using Otus.Teaching.PromoCodeFactory.WebHost.Models;
using Otus.Teaching.PromoCodeFactory.WebHost.Services;
using Otus.Teaching.PromoCodeFactory.WebHost.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Otus.Teaching.PromoCodeFactory.UnitTests.WebHost.Controllers.Partners
{
    public class SetPartnerPromoCodeLimitAsyncTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<IRepository<Partner>> _mockPartnerRepository;

        public SetPartnerPromoCodeLimitAsyncTests()
        {
            var services = new ServiceCollection();
            _mockPartnerRepository = new Mock<IRepository<Partner>>();
            services.AddSingleton(_mockPartnerRepository.Object);
            services.AddPatnerService();

            _serviceProvider = services.BuildServiceProvider();
        }

        //#1
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_PartnerNotFound_ReturnsNotFoundResult()
        {
            // Arrange
            var autofixture = new Fixture();
            var partnerId = autofixture.Create<Guid>();

            _mockPartnerRepository.Reset();
            _mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync((Partner)null);

            var partnerService = _serviceProvider.GetService<IPartnerService>();

            var setPartnerPromoCodeLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1);
            var controller = new PartnersController(_mockPartnerRepository.Object, partnerService);

            // Act
            var result = await controller.SetPartnerPromoCodeLimitAsync(partnerId, setPartnerPromoCodeLimitRequest);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        //#2
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_PartnerIsInactive_ReturnsBadRequestResult()
        {
            //Arrange
            var fixture = CreateFixture();

            var partnerId = fixture.Create<Guid>();
            var inactivePartner = fixture.Build<Partner>()
                                          .With(p => p.Id, partnerId)
                                          .With(p => p.IsActive, false)
                                          .Create();

            _mockPartnerRepository.Reset();
            _mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId)).ReturnsAsync(inactivePartner);
            var partnerService = _serviceProvider.GetRequiredService<IPartnerService>();

            var controller = new PartnersController(_mockPartnerRepository.Object, partnerService);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1);

            // Act
            var result = await controller.SetPartnerPromoCodeLimitAsync(partnerId, newLimitRequest);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        //#3
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_NewLimitSet_NumberIssuedPromoCodesResetsToZero()
        {
            // Arrange
            var fixture = CreateFixture();
            var partnerId = fixture.Create<Guid>();

            var existingLimit = fixture.Build<PartnerPromoCodeLimit>()
                                                    .With(l => l.Limit, 50)
                                                    .With(l => l.CreateDate, DateTime.UtcNow.AddDays(-10))
                                                    .With(l => l.EndDate, DateTime.UtcNow.AddDays(10))
                                                    .Without(l => l.CancelDate)
                                                    .Create();

            var activePartner = fixture.Build<Partner>()
                                           .With(p => p.Id, partnerId)
                                           .With(p => p.IsActive, true)
                                           .With(p => p.NumberIssuedPromoCodes, 10)
                                           .Without(p => p.PartnerLimits)
                                           .Create();

            activePartner.PartnerLimits = new List<PartnerPromoCodeLimit> { existingLimit };

            _mockPartnerRepository.Reset();
            _mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            _mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                                .Callback<Partner>(p => activePartner = p)
                                .Returns(Task.CompletedTask);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1);

            var partnerService = _serviceProvider.GetRequiredService<IPartnerService>();
            var controller = new PartnersController(_mockPartnerRepository.Object, partnerService);

            // Act
            await controller.SetPartnerPromoCodeLimitAsync(partnerId, newLimitRequest);

            // Assert
            activePartner.NumberIssuedPromoCodes.Should().Be(0);
            activePartner.PartnerLimits.Should().HaveCount(2);
            activePartner.PartnerLimits.Last().Limit.Should().Be(newLimitRequest.Limit);
        }

        //#3
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_ExpiredLimitSet_NumberIssuedPromoCodesDoesNotReset()
        {
            // Arrange
            var cancelledDate = DateTime.UtcNow.AddDays(-5); // Дата отмены лимита

            var fixture = CreateFixture();
            var partnerId = fixture.Create<Guid>();

            var existingLimit = fixture.Build<PartnerPromoCodeLimit>()
                                                        .With(l => l.Limit, 50)
                                                        .With(l => l.CreateDate, DateTime.UtcNow.AddDays(-10))
                                                        .With(l => l.EndDate, cancelledDate)
                                                        .With(l => l.CancelDate, cancelledDate)
                                                        .Create();

            var activePartner = fixture.Build<Partner>()
                                           .With(p => p.Id, partnerId)
                                           .With(p => p.IsActive, true)
                                           .With(p => p.NumberIssuedPromoCodes, 10)
                                           .Without(p => p.PartnerLimits)
                                           .Create();

            activePartner.PartnerLimits = new List<PartnerPromoCodeLimit> { existingLimit };

            _mockPartnerRepository.Reset();
            _mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            _mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                                 .Callback<Partner>(p => activePartner = p)
                                 .Returns(Task.CompletedTask);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1); // Новый лимит с количеством и датой окончания

            var partnerService = _serviceProvider.GetRequiredService<IPartnerService>();
            var controller = new PartnersController(_mockPartnerRepository.Object, partnerService);

            // Act
            await controller.SetPartnerPromoCodeLimitAsync(partnerId, newLimitRequest);

            // Assert
            activePartner.NumberIssuedPromoCodes.Should().NotBe(0); // Количество выданных промокодов не должно обнуляться
            activePartner.PartnerLimits.Should().HaveCount(2); // Убедитесь, что список лимитов обновлён корректно
            activePartner.PartnerLimits.Last().Limit.Should().Be(newLimitRequest.Limit); // Проверяем, что новый лимит установлен корректно
        }

        //#4
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_WithExistingLimit_DisablesPreviousLimit()
        {
            // Arrange

            var numberIssuedPromoCodes = 123;

            var newLimit = 56; // Новый лимит, который будет установлен

            var fixture = CreateFixture();
            var partnerId = fixture.Create<Guid>();

            var existingLimit = fixture.Build<PartnerPromoCodeLimit>()
                                       .With(l => l.Limit, 1000)
                                       .With(l => l.CreateDate, DateTime.UtcNow.AddDays(-10))
                                       .With(l => l.EndDate, DateTime.UtcNow.AddDays(10))
                                       .Without(l => l.CancelDate)
                                       .Create();

            var activePartner = fixture.Build<Partner>()
                                       .With(p => p.Id, partnerId)
                                       .With(p => p.IsActive, true)
                                       .With(p => p.NumberIssuedPromoCodes, numberIssuedPromoCodes)
                                       .Without(p => p.PartnerLimits)
                                       .Create();

            activePartner.PartnerLimits = new List<PartnerPromoCodeLimit> { existingLimit };
            
            _mockPartnerRepository.Reset();
            _mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);
            _mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                                             .Callback<Partner>(p => activePartner = p)
                                             .Returns(Task.CompletedTask);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(newLimit, 30); // Новый лимит, который будет установлен
            var partnerService = _serviceProvider.GetRequiredService<IPartnerService>();
            var controller = new PartnersController(_mockPartnerRepository.Object, partnerService);

            // Act
            await controller.SetPartnerPromoCodeLimitAsync(partnerId, newLimitRequest);

            // Assert
            activePartner.PartnerLimits.ToList().First().Should().HasProperty(nameof(PartnerPromoCodeLimit.CancelDate));
            activePartner.PartnerLimits.Should().HaveCount(2); // Убедитесь, что новый лимит добавлен
            activePartner.PartnerLimits.Last().Limit.Should().Be(newLimit); // Убедитесь, что новый лимит установлен корректно
        }

        //#5
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_LimitIsZeroOrNegative_ReturnsBadRequest()
        {
            // Arrange
            var fixture = CreateFixture();
            var partnerId = fixture.Create<Guid>();

            var existingLimit = fixture.Build<PartnerPromoCodeLimit>()
                           .With(l => l.Limit, 1000)
                           .With(l => l.CreateDate, DateTime.UtcNow.AddDays(-10))
                           .With(l => l.EndDate, DateTime.UtcNow.AddDays(10))
                           .Without(l => l.CancelDate)
                           .Create();

            var activePartner = fixture.Build<Partner>()
                                       .With(p => p.Id, partnerId)
                                       .With(p => p.IsActive, true)
                                       .With(p => p.NumberIssuedPromoCodes, 10)
                                       .Without(p => p.PartnerLimits)
                                       .Create();

            activePartner.PartnerLimits = new List<PartnerPromoCodeLimit> { existingLimit };

            _mockPartnerRepository.Reset();
            _mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId)).ReturnsAsync(activePartner);
            _mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                     .Callback<Partner>(p => activePartner = p)
                     .Returns(Task.CompletedTask);

            var invalidLimitRequest = CreateSetPartnerPromoCodeLimitRequest(0, 30); // Лимит равен 0, что невалидно

            var partnerService = _serviceProvider.GetRequiredService<IPartnerService>();
            var controller = new PartnersController(_mockPartnerRepository.Object, partnerService);

            // Act
            var result = await controller.SetPartnerPromoCodeLimitAsync(partnerId, invalidLimitRequest);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>(); // Используем FluentAssertions для проверки
        }

        //#5

        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_LimitIsPositive_AddsLimitSuccessfully()
        {
            // Arrange
            var fixture = CreateFixture();
            var partnerId = fixture.Create<Guid>();

            var existingLimit = fixture.Build<PartnerPromoCodeLimit>()
                           .With(l => l.Limit, 1000)
                           .With(l => l.CreateDate, DateTime.UtcNow.AddDays(-10))
                           .With(l => l.EndDate, DateTime.UtcNow.AddDays(10))
                           .Without(l => l.CancelDate)
                           .Create();

            var activePartner = fixture.Build<Partner>()
                                       .With(p => p.Id, partnerId)
                                       .With(p => p.IsActive, true)
                                       .With(p => p.NumberIssuedPromoCodes, 10)
                                       .Without(p => p.PartnerLimits)
                                       .Create();

            activePartner.PartnerLimits = new List<PartnerPromoCodeLimit> { existingLimit };
            _mockPartnerRepository.Reset();
            _mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId)).ReturnsAsync(activePartner);
            _mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                     .Callback<Partner>(p => activePartner = p)
                     .Returns(Task.CompletedTask);

            var validLimitRequest = CreateSetPartnerPromoCodeLimitRequest(1, 30); // Лимит положителен
            var partnerService = _serviceProvider.GetRequiredService<IPartnerService>();
            var controller = new PartnersController(_mockPartnerRepository.Object, partnerService);

            // Act
            var result = await controller.SetPartnerPromoCodeLimitAsync(partnerId, validLimitRequest);

            // Assert
            result.Should().BeOfType<CreatedAtActionResult>(); // или другой тип результата, в зависимости от вашей реализации
        }

        //#6
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_SavesNewLimitInDatabase()
        {
            // Arrange
            var fixture = CreateFixture();
            var partnerId = fixture.Create<Guid>();

            var existingLimit = fixture.Build<PartnerPromoCodeLimit>()
                           .With(l => l.Limit, 1000)
                           .With(l => l.CreateDate, DateTime.UtcNow.AddDays(-10))
                           .With(l => l.EndDate, DateTime.UtcNow.AddDays(10))
                           .Without(l => l.CancelDate)
                           .Create();

            var activePartner = fixture.Build<Partner>()
                                       .With(p => p.Id, partnerId)
                                       .With(p => p.IsActive, true)
                                       .With(p => p.NumberIssuedPromoCodes, 10)
                                       .Without(p => p.PartnerLimits)
                                       .Create();

            activePartner.PartnerLimits = new List<PartnerPromoCodeLimit> { existingLimit };

            _mockPartnerRepository.Reset();
            _mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId)).ReturnsAsync(activePartner);
            _mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                     .Callback<Partner>(p => activePartner = p)
                     .Returns(Task.CompletedTask);

            var partnerService = _serviceProvider.GetRequiredService<IPartnerService>();
            var controller = new PartnersController(_mockPartnerRepository.Object, partnerService);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(1, 30); // Новый лимит с количеством и датой окончания

            // Act
            await controller.SetPartnerPromoCodeLimitAsync(partnerId, newLimitRequest);

            // Assert
            _mockPartnerRepository.Verify(repo => repo.UpdateAsync(activePartner), Times.Once); // Проверяем, что метод UpdateAsync был вызван один раз
        }

        private SetPartnerPromoCodeLimitRequest CreateSetPartnerPromoCodeLimitRequest(int limit, int daysToAdd)
        {
            var fixture = CreateFixture();

            var newLimitRequest = fixture.Build<SetPartnerPromoCodeLimitRequest>()
                .With(l => l.Limit, limit)
                .With(l => l.EndDate, DateTime.Now.AddDays(daysToAdd))
                .Create();

            return newLimitRequest;
        }


        private Fixture CreateFixture()
        {
            var fixture = new Fixture();
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            return fixture;
        }

    }
}