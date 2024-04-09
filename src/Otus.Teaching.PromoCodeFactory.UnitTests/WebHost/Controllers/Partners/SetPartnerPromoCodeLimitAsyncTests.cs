using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Namotion.Reflection;
using Otus.Teaching.PromoCodeFactory.Core.Abstractions.Repositories;
using Otus.Teaching.PromoCodeFactory.Core.Domain.PromoCodeManagement;
using Otus.Teaching.PromoCodeFactory.WebHost.Controllers;
using Otus.Teaching.PromoCodeFactory.WebHost.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Otus.Teaching.PromoCodeFactory.UnitTests.WebHost.Controllers.Partners
{
    public class SetPartnerPromoCodeLimitAsyncTests
    {
        //#1
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_PartnerNotFound_ReturnsNotFoundResult()
        {
            // Arrange
            var autofixture = new Fixture();
            var partnerId = autofixture.Create<Guid>();

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync((Partner)null);

            var setPartnerPromoCodeLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1);
            var controller = new PartnersController(mockPartnerRepository.Object);

            // Act
            var result = await controller.SetPartnerPromoCodeLimitAsync(partnerId, setPartnerPromoCodeLimitRequest);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
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

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId)).ReturnsAsync(inactivePartner);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1);

            var controller = new PartnersController(mockPartnerRepository.Object);

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

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                                .Callback<Partner>(p => activePartner = p)
                                .Returns(Task.CompletedTask);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1);

            var controller = new PartnersController(mockPartnerRepository.Object);

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

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                                 .Callback<Partner>(p => activePartner = p)
                                 .Returns(Task.CompletedTask);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1); // Новый лимит с количеством и датой окончания

            var controller = new PartnersController(mockPartnerRepository.Object);

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

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                                 .Callback<Partner>(p => activePartner = p)
                                 .Returns(Task.CompletedTask);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(newLimit, 30); // Новый лимит, который будет установлен

            var controller = new PartnersController(mockPartnerRepository.Object);

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

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId)).ReturnsAsync(activePartner);
            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                     .Callback<Partner>(p => activePartner = p)
                     .Returns(Task.CompletedTask);

            var invalidLimitRequest = CreateSetPartnerPromoCodeLimitRequest(0, 30); // Лимит равен 0, что невалидно

            var controller = new PartnersController(mockPartnerRepository.Object);

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

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId)).ReturnsAsync(activePartner);
            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                     .Callback<Partner>(p => activePartner = p)
                     .Returns(Task.CompletedTask);

            var validLimitRequest = CreateSetPartnerPromoCodeLimitRequest(1, 30); // Лимит положителен

            var controller = new PartnersController(mockPartnerRepository.Object);

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

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId)).ReturnsAsync(activePartner);
            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                     .Callback<Partner>(p => activePartner = p)
                     .Returns(Task.CompletedTask);


            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(1, 30); // Новый лимит с количеством и датой окончания

            var controller = new PartnersController(mockPartnerRepository.Object);

            // Act
            await controller.SetPartnerPromoCodeLimitAsync(partnerId, newLimitRequest);

            // Assert
            mockPartnerRepository.Verify(repo => repo.UpdateAsync(activePartner), Times.Once); // Проверяем, что метод UpdateAsync был вызван один раз
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