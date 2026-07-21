using Meridian.ServiceDefaults;

// Receipts API skeleton. Stage 0: authenticated but not yet a PEP. In Stage 4 this
// service gains AuthZen.Pep and delegates every decision to the shared PDP,
// proving one policy enforced across multiple services.
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddMeridianApiAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Meridian Receipts API — replace with real endpoints.")
   .RequireAuthorization();

app.Run();
