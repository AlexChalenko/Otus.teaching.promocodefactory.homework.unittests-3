using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
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
        //TODO: Add Unit Tests

        //#1
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_PartnerNotFound_ReturnsNotFoundResult()
        {
            // Arrange
            var partnerId = Guid.NewGuid();
            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync((Partner)null);

            var setPartnerPromoCodeLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1);

            var controller = new PartnersController(mockPartnerRepository.Object);

            // Act
            var result = await controller.SetPartnerPromoCodeLimitAsync(partnerId, setPartnerPromoCodeLimitRequest);

            // Assert
            result.Should().BeOfType<NotFoundResult>(); // Использование FluentAssertions
        }

        //#2
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_PartnerIsInactive_ReturnsBadRequestResult()
        {
            // Arrange
            var partnerId = Guid.NewGuid();
            var mockPartnerRepository = new Mock<IRepository<Partner>>();

            var inactivePartner = new Partner
            {
                Id = partnerId,
                IsActive = false,
            };

            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(inactivePartner);

            var setPartnerPromoCodeLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1);

            var controller = new PartnersController(mockPartnerRepository.Object);

            // Act
            var result = await controller.SetPartnerPromoCodeLimitAsync(partnerId, setPartnerPromoCodeLimitRequest);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>(); // Используем FluentAssertions для проверки результата
        }

        //#3
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_NewLimitSet_NumberIssuedPromoCodesResetsToZero()
        {
            // Arrange
            var partnerId = Guid.NewGuid();
            var activePartner = new Partner
            {
                Id = partnerId,
                IsActive = true,
                NumberIssuedPromoCodes = 10, // Исходное количество выданных промокодов
                                             // Задайте другие необходимые свойства объекта Partner здесь
                PartnerLimits = new List<PartnerPromoCodeLimit>
                {
                    new PartnerPromoCodeLimit { Id = Guid.NewGuid(), PartnerId = partnerId, Limit = 5, CreateDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(10) }
                }
            };

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            // Подразумевается, что после вызова UpdateAsync объект activePartner будет обновлён
            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                                 .Callback<Partner>(p =>
                                 {
                                     activePartner.NumberIssuedPromoCodes = p.NumberIssuedPromoCodes;
                                     activePartner.PartnerLimits = p.PartnerLimits;
                                     // Обновите другие свойства при необходимости
                                 })
                                 .Returns(Task.CompletedTask); // Поскольку метод асинхронный, возвращаем завершенную задачу

            var setPartnerPromoCodeLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 1);

            var controller = new PartnersController(mockPartnerRepository.Object);

            // Act
            await controller.SetPartnerPromoCodeLimitAsync(partnerId, setPartnerPromoCodeLimitRequest);

            // Assert
            activePartner.NumberIssuedPromoCodes.Should().Be(0); // Используем FluentAssertions для проверки
            activePartner.PartnerLimits.Should().HaveCount(2); // Убедитесь, что список лимитов обновлён корректно
            activePartner.PartnerLimits.Last().Limit.Should().Be(setPartnerPromoCodeLimitRequest.Limit); // Проверяем, что новый лимит установлен корректно
        }

        //#3
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_ExpiredLimitSet_NumberIssuedPromoCodesDoesNotReset()
        {
            // Arrange
            var partnerId = Guid.NewGuid();
            var issuedPromoCodesCount = 10; // Предположим, что партнер уже выдал 10 промокодов
            var cancelledDate = DateTime.UtcNow.AddDays(-5); // Дата отмены лимита
            var activePartner = new Partner
            {
                Id = partnerId,
                IsActive = true,
                NumberIssuedPromoCodes = issuedPromoCodesCount,
                PartnerLimits = new List<PartnerPromoCodeLimit>
                {
                    new PartnerPromoCodeLimit
                    {
                        Id = Guid.NewGuid(),
                        PartnerId = partnerId,
                        Limit = issuedPromoCodesCount,
                        CreateDate = DateTime.UtcNow.AddDays(-20),
                        EndDate = cancelledDate,
                        CancelDate = cancelledDate
                    }
                }
            };

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                                 .Callback<Partner>(p =>
                                 {
                                     activePartner = p;
                                 })
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
            var partnerId = Guid.NewGuid();
            var existingLimit = new PartnerPromoCodeLimit
            {
                Id = Guid.NewGuid(),
                PartnerId = partnerId,
                Limit = 5,
                CreateDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(10) // Предположим, что лимит еще активен
            };

            var activePartner = new Partner
            {
                Id = partnerId,
                IsActive = true,
                PartnerLimits = new List<PartnerPromoCodeLimit> { existingLimit }
            };

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
                                 .Callback<Partner>(p => activePartner = p)
                                 .Returns(Task.CompletedTask);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(100, 30); // Новый лимит, который будет установлен

            var controller = new PartnersController(mockPartnerRepository.Object);

            // Act
            await controller.SetPartnerPromoCodeLimitAsync(partnerId, newLimitRequest);

            // Assert
            activePartner.PartnerLimits.Should().ContainSingle(limit => limit.CancelDate.HasValue); // Проверяем, что у предыдущего лимита установлена дата отмены
            activePartner.PartnerLimits.Should().ContainSingle(limit => limit.Id == existingLimit.Id && limit.CancelDate.HasValue); // Более точная проверка, что это именно предыдущий лимит был отключен
            activePartner.PartnerLimits.Should().HaveCount(2); // Убедитесь, что новый лимит добавлен
        }

        //#5
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_LimitIsZeroOrNegative_ReturnsBadRequest()
        {
            // Arrange
            var partnerId = Guid.NewGuid();
            var activePartner = new Partner
            {
                Id = partnerId,
                IsActive = true,
                NumberIssuedPromoCodes = 10, // Исходное количество выданных промокодов
                                             // Задайте другие необходимые свойства объекта Partner здесь
                PartnerLimits = new List<PartnerPromoCodeLimit>
                {
                    new PartnerPromoCodeLimit { Id = Guid.NewGuid(), PartnerId = partnerId, Limit = 5, CreateDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(10) }
                }
            };

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

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
            var partnerId = Guid.NewGuid();
            var activePartner = new Partner
            {
                Id = partnerId,
                IsActive = true,
                NumberIssuedPromoCodes = 10, // Исходное количество выданных промокодов
                                             // Задайте другие необходимые свойства объекта Partner здесь
                PartnerLimits = new List<PartnerPromoCodeLimit>
                {
                    new PartnerPromoCodeLimit { Id = Guid.NewGuid(), PartnerId = partnerId, Limit = 5, CreateDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(10) }
                }
            };

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            mockPartnerRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Partner>()))
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
            var partnerId = Guid.NewGuid();
            var activePartner = new Partner
            {
                Id = partnerId,
                IsActive = true,
                NumberIssuedPromoCodes = 10, // Исходное количество выданных промокодов
                                             // Задайте другие необходимые свойства объекта Partner здесь
                PartnerLimits = new List<PartnerPromoCodeLimit>
                {
                    new PartnerPromoCodeLimit { Id = Guid.NewGuid(), PartnerId = partnerId, Limit = 5, CreateDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(10) }
                }
            };

            var mockPartnerRepository = new Mock<IRepository<Partner>>();
            mockPartnerRepository.Setup(repo => repo.GetByIdAsync(partnerId))
                                 .ReturnsAsync(activePartner);

            var newLimitRequest = CreateSetPartnerPromoCodeLimitRequest(1, 30); // Новый лимит с количеством и датой окончания

            var controller = new PartnersController(mockPartnerRepository.Object);

            // Act
            await controller.SetPartnerPromoCodeLimitAsync(partnerId, newLimitRequest);

            // Assert
            mockPartnerRepository.Verify(repo => repo.UpdateAsync(activePartner), Times.Once); // Проверяем, что метод UpdateAsync был вызван один раз
        }

        private SetPartnerPromoCodeLimitRequest CreateSetPartnerPromoCodeLimitRequest(int limit, int daysToAdd)
        {
            return new SetPartnerPromoCodeLimitRequest()
            {
                Limit = limit,
                EndDate = DateTime.Now.AddDays(daysToAdd)
            };
        }
    }
}