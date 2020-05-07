// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.IO;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class CustomDataDirectoryTest
    {
        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(customPath))
                Directory.Delete(customPath, true);
        }

        [Test]
        public void TestDefaultDirectory()
        {
            using (HeadlessGameHost host = new CleanRunHeadlessGameHost(nameof(TestDefaultDirectory)))
            {
                try
                {
                    var osu = loadOsu(host);
                    var storage = osu.Dependencies.Get<Storage>();

                    string defaultStorageLocation = Path.Combine(Environment.CurrentDirectory, "headless", nameof(TestDefaultDirectory));

                    Assert.That(storage.GetFullPath("."), Is.EqualTo(defaultStorageLocation));
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        private string customPath => Path.Combine(Environment.CurrentDirectory, "custom-path");

        [Test]
        public void TestCustomDirectory()
        {
            using (var host = new HeadlessGameHost(nameof(TestCustomDirectory)))
            {
                string headlessPrefix = Path.Combine("headless", nameof(TestCustomDirectory));

                // need access before the game has constructed its own storage yet.
                Storage storage = new DesktopStorage(headlessPrefix, host);
                // manual cleaning so we can prepare a config file.
                storage.DeleteDirectory(string.Empty);

                using (var storageConfig = new StorageConfigManager(storage))
                    storageConfig.Set(StorageConfig.FullPath, customPath);

                try
                {
                    var osu = loadOsu(host);

                    // switch to DI'd storage
                    storage = osu.Dependencies.Get<Storage>();

                    Assert.That(storage.GetFullPath("."), Is.EqualTo(customPath));
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        [Test]
        public void TestMigration()
        {
            using (HeadlessGameHost host = new CleanRunHeadlessGameHost(nameof(TestMigration)))
            {
                try
                {
                    var osu = loadOsu(host);
                    var storage = osu.Dependencies.Get<Storage>();

                    // ensure we perform a save
                    host.Dependencies.Get<FrameworkConfigManager>().Save();

                    // ensure we "use" cache
                    host.Storage.GetStorageForDirectory("cache");

                    string defaultStorageLocation = Path.Combine(Environment.CurrentDirectory, "headless", nameof(TestMigration));

                    Assert.That(storage.GetFullPath("."), Is.EqualTo(defaultStorageLocation));

                    (storage as OsuStorage)?.Migrate(customPath);

                    Assert.That(storage.GetFullPath("."), Is.EqualTo(customPath));

                    foreach (var file in OsuStorage.IGNORE_FILES)
                    {
                        Assert.That(host.Storage.Exists(file), Is.True);
                        Assert.That(storage.Exists(file), Is.False);
                    }

                    foreach (var dir in OsuStorage.IGNORE_DIRECTORIES)
                    {
                        Assert.That(host.Storage.ExistsDirectory(dir), Is.True);
                        Assert.That(storage.ExistsDirectory(dir), Is.False);
                    }

                    Assert.That(new StreamReader(host.Storage.GetStream("storage.ini")).ReadToEnd().Contains($"FullPath = {customPath}"));
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        private OsuGameBase loadOsu(GameHost host)
        {
            var osu = new OsuGameBase();
            Task.Run(() => host.Run(osu));
            waitForOrAssert(() => osu.IsLoaded, @"osu! failed to start in a reasonable amount of time");
            return osu;
        }

        private static void waitForOrAssert(Func<bool> result, string failureMessage, int timeout = 60000)
        {
            Task task = Task.Run(() =>
            {
                while (!result()) Thread.Sleep(200);
            });

            Assert.IsTrue(task.Wait(timeout), failureMessage);
        }
    }
}
