using Meridian.ExpensePortal.Controllers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.UnitTests.ExpensePortal.Controllers;

public class AccountControllerTests
{
    private const string CustomReturnUrl = "/expenses";
    private const string ExternalReturnUrl = "https://evil.example";
    private const string DefaultRedirect = "/";

    // Controller.Url isn't populated outside the MVC action-invocation pipeline,
    // so Login's Url.IsLocalUrl call needs a stand-in that mimics the real
    // UrlHelper: local paths (single leading slash) are safe, anything else isn't.
    private static AccountController CreateController()
    {
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string>()))
            .Returns<string>(url => url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\"));
        return new AccountController { Url = urlHelper.Object };
    }

    [Fact]
    public void Login_WithLocalReturnUrl_ChallengesOidcWithThatRedirectUri()
    {
        var result = CreateController().Login(CustomReturnUrl);

        var challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.AuthenticationSchemes.Should().ContainSingle(OpenIdConnectDefaults.AuthenticationScheme);
        challenge.Properties.Should().NotBeNull();
        challenge.Properties!.RedirectUri.Should().Be(CustomReturnUrl);
    }

    [Fact]
    public void Login_WithoutReturnUrl_ChallengesOidcWithDefaultRedirect()
    {
        var result = CreateController().Login(returnUrl: null);

        var challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.Properties!.RedirectUri.Should().Be(DefaultRedirect);
    }

    [Fact]
    public void Login_WithExternalReturnUrl_FallsBackToDefaultRedirect()
    {
        var result = CreateController().Login(ExternalReturnUrl);

        var challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.Properties!.RedirectUri.Should().Be(DefaultRedirect);
    }

    [Fact]
    public void Logout_SignsOutCookieAndOidcSchemesWithDefaultRedirect()
    {
        var result = CreateController().Logout();

        var signOut = result.Should().BeOfType<SignOutResult>().Subject;
        signOut.AuthenticationSchemes.Should().BeEquivalentTo(
        [
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme
        ]);
        signOut.Properties!.RedirectUri.Should().Be(DefaultRedirect);
    }
}
