using LvvErp.Infrastructure.Files;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace LvvErp.Api.Infrastructure;

public static class StoredFilesSetup
{
    /// <summary>
    /// Serves uploaded files (employee photos) from local storage on their own branch. They are
    /// public so that &lt;img&gt; tags work without a token; file names are random UUIDs.
    /// A missing file ends in 404 here instead of falling through to the auth-protected pipeline.
    /// </summary>
    public static IApplicationBuilder UseStoredFiles(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
        var root = LocalFileStorage.ResolveRoot(options, app.Environment);
        Directory.CreateDirectory(root);

        return app.Map(options.PublicBasePath.TrimEnd('/'), files =>
        {
            files.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(root),
                OnPrepareResponse = ctx =>
                {
                    // Names are immutable (a new photo gets a new name), so cache aggressively.
                    ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
                },
            });
            files.Run(ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            });
        });
    }
}
