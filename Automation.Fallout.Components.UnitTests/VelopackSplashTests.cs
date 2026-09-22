using Automation.Fallout.Components.Components;

namespace Automation.Fallout.Components.UnitTests;

/// <summary>
/// Which splash Setup.exe shows. The rule worth pinning down is the one about the MeddlingIdiot
/// logo: it is all rights reserved, and this package is public, so it must only ever be the
/// default for MeddlingIdiot's own installers.
/// </summary>
public class VelopackSplashTests
{
    private static readonly string Root = Path.GetTempPath();

    [Test]
    public async Task MeddlingIdiot_builds_get_the_logo_by_default()
    {
        var choice = VelopackSplash.Choose("", "meddlingidiotinstallers", Root);

        await Assert.That(choice.Source).IsEqualTo(VelopackSplashSource.MeddlingIdiotDefault);
    }

    [Test]
    public async Task The_account_match_ignores_case()
    {
        var choice = VelopackSplash.Choose(null, "MeddlingIdiotInstallers", Root);

        await Assert.That(choice.Source).IsEqualTo(VelopackSplashSource.MeddlingIdiotDefault);
    }

    [Test]
    public async Task Anyone_else_keeps_the_Velopack_splash()
    {
        var choice = VelopackSplash.Choose("", "staftrinstallers", Root);

        using (Assert.Multiple())
        {
            await Assert.That(choice.Source).IsEqualTo(VelopackSplashSource.VelopackDefault);
            await Assert.That(choice.Path).IsNull();
        }
    }

    [Test]
    public async Task A_configured_image_overrides_the_logo()
    {
        var image = Path.Combine(Root, $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(image, [1]);
        try
        {
            var choice = VelopackSplash.Choose(image, "meddlingidiotinstallers", Root);

            using (Assert.Multiple())
            {
                await Assert.That(choice.Source).IsEqualTo(VelopackSplashSource.Configured);
                await Assert.That(choice.Path).IsEqualTo(image);
            }
        }
        finally
        {
            File.Delete(image);
        }
    }

    [Test]
    public async Task A_relative_path_resolves_against_the_repo_root()
    {
        var name = $"{Guid.NewGuid():N}.png";
        await File.WriteAllBytesAsync(Path.Combine(Root, name), [1]);
        try
        {
            var choice = VelopackSplash.Choose(name, "staftrinstallers", Root);

            await Assert.That(choice.Path).IsEqualTo(Path.GetFullPath(name, Root));
        }
        finally
        {
            File.Delete(Path.Combine(Root, name));
        }
    }

    [Test]
    public async Task None_turns_the_logo_off()
    {
        var choice = VelopackSplash.Choose("None", "meddlingidiotinstallers", Root);

        await Assert.That(choice.Source).IsEqualTo(VelopackSplashSource.VelopackDefault);
    }

    [Test]
    public async Task A_missing_configured_image_warns_and_falls_back()
    {
        var choice = VelopackSplash.Choose("assets/no-such-splash.png", "meddlingidiotinstallers", Root);

        using (Assert.Multiple())
        {
            await Assert.That(choice.Source).IsEqualTo(VelopackSplashSource.MeddlingIdiotDefault);
            await Assert.That(choice.ConfiguredPathMissing).IsTrue();
            await Assert.That(choice.Reason).Contains("no-such-splash.png");
        }
    }

    [Test]
    public async Task The_embedded_logo_is_a_png()
    {
        var destination = Path.Combine(Root, $"{Guid.NewGuid():N}", "splash.png");
        try
        {
            VelopackSplash.WriteMeddlingIdiotSplash(destination);

            var header = (await File.ReadAllBytesAsync(destination)).Take(8).ToArray();
            await Assert.That(header).IsEquivalentTo(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(destination)!, recursive: true);
        }
    }
}
