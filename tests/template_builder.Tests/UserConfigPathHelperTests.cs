using System.Runtime.InteropServices;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class UserConfigPathHelperTests {
	[Fact]
	public void ResolveBasePath_OnWindows_ReturnsAppData() {
		var path = UserConfigPathHelper.ResolveBasePath(
			p => p == OSPlatform.Windows,
			userProfile: @"C:\Users\alice");

		Assert.Equal(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			path);
	}

	[Fact]
	public void ResolveBasePath_OnLinux_ReturnsDotConfigUnderUserProfile() {
		var path = UserConfigPathHelper.ResolveBasePath(
			p => p == OSPlatform.Linux,
			userProfile: "/home/alice");

		Assert.Equal(Path.Combine("/home/alice", ".config"), path);
	}

	[Fact]
	public void ResolveBasePath_OnOSX_ReturnsLibraryApplicationSupport() {
		var path = UserConfigPathHelper.ResolveBasePath(
			p => p == OSPlatform.OSX,
			userProfile: "/Users/alice");

		Assert.Equal(Path.Combine("/Users/alice", "Library", "Application Support"), path);
	}

	[Fact]
	public void ResolveBasePath_OnUnknownPlatform_Throws() {
		Assert.Throws<PlatformNotSupportedException>(() =>
			UserConfigPathHelper.ResolveBasePath(_ => false, userProfile: "/anywhere"));
	}
}
