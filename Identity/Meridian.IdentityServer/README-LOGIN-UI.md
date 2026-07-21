# Login UI required

This host wires up Duende IdentityServer with in-memory config and test users,
but interactive login needs the Duende UI (login/consent/logout pages). Add it with:

    dotnet new install Duende.IdentityServer.Templates
    dotnet new isui        # run inside this project folder

Duende IdentityServer is free for development/testing and for small companies,
but a license is required for production. See https://duendesoftware.com/products/identityserver
