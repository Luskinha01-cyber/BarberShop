using BarberShop.Controllers;
using BarberShop.Data;
using BarberShop.Models;
using BarberShop.ViewModels.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BarberShop.Tests;

public class AuthenticationTests
{
    [Fact]
    public async Task Register_ValidClient_CreatesUserAndAssignsClientRole()
    {
        // Arrange
        var context = TestHelper.CreateInMemoryDbContext();
        var mockUserManager = TestHelper.CreateMockUserManager();
        var mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            mockUserManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), SeedData.ClientRole))
            .ReturnsAsync(IdentityResult.Success);

        var controller = new AccountController(
            mockUserManager.Object,
            mockSignInManager.Object,
            context,
            new Mock<ILogger<AccountController>>().Object);

        var model = new RegisterViewModel
        {
            FullName = "Carlos Eduardo",
            PhoneNumber = "(11) 98765-4321",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            AcceptTerms = true,
            AcceptPrivacy = true,
            WhatsAppConsent = true
        };

        // Act
        var result = await controller.Register(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Booking", redirect.ControllerName);

        mockUserManager.Verify(m => m.CreateAsync(It.Is<ApplicationUser>(u =>
            u.PhoneNumber == "5511987654321" &&
            u.FullName == "Carlos Eduardo" &&
            u.WhatsAppConsent == true), "Password123!"), Times.Once);

        mockUserManager.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), SeedData.ClientRole), Times.Once);
    }

    [Fact]
    public async Task Register_DuplicatePhoneNumber_ReturnsModelErrorAndRejects()
    {
        // Arrange
        var context = TestHelper.CreateInMemoryDbContext();
        // Adiciona usuário existente com o mesmo telefone normalizado
        context.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "5511987654321",
            PhoneNumber = "5511987654321",
            FullName = "Cliente Existente",
            WhatsAppConsent = true
        });
        await context.SaveChangesAsync();

        var mockUserManager = TestHelper.CreateMockUserManager();
        var mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            mockUserManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        var controller = new AccountController(
            mockUserManager.Object,
            mockSignInManager.Object,
            context,
            new Mock<ILogger<AccountController>>().Object);

        var model = new RegisterViewModel
        {
            FullName = "Outro Cliente",
            PhoneNumber = "(11) 98765-4321",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            AcceptTerms = true,
            AcceptPrivacy = true,
            WhatsAppConsent = true
        };

        // Act
        var result = await controller.Register(model);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("PhoneNumber"));
        Assert.Contains("Já existe uma conta cadastrada com este número de celular", 
            controller.ModelState["PhoneNumber"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsModelError()
    {
        // Arrange
        var context = TestHelper.CreateInMemoryDbContext();
        var mockUserManager = TestHelper.CreateMockUserManager();
        var mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            mockUserManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        var controller = new AccountController(
            mockUserManager.Object,
            mockSignInManager.Object,
            context,
            new Mock<ILogger<AccountController>>().Object);

        var model = new LoginViewModel
        {
            Identifier = "11988887777",
            Password = "WrongPassword"
        };

        // Act
        var result = await controller.Login(model);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("inválidos"));
    }
}
