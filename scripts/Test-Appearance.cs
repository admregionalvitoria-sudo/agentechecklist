using System;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using ChecklistLogin;
class AppearanceTests {
    static int Main() {
        var parse = typeof(RemoteAppearance).GetMethod("Parse", BindingFlags.Static | BindingFlags.NonPublic);
        string valid = "{\"title\":\"Laboratório SENAI\",\"subtitle\":\"Bem-vindo\",\"notice\":\"Aviso\",\"accent\":\"#123ABC\",\"image\":\"\"}";
        var data = (Dictionary<string,string>)parse.Invoke(null, new object[] { valid });
        if (data["title"] != "Laboratório SENAI") throw new Exception("Unicode mismatch");
        foreach (var invalid in new[] { "{}", valid.Replace("#123ABC", "red"), valid.Replace("Laboratório SENAI", ""), valid.Replace("Aviso", new string('a', 2001)), valid.Replace("Bem-vindo", new string('b', 121)) }) {
            bool rejected = false;
            try { parse.Invoke(null, new object[] { invalid }); } catch (TargetInvocationException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid appearance accepted");
        }
        RemoteAppearance.Current = data;
        if (RemoteAppearance.Get("title", "fallback") != data["title"]) throw new Exception("Lookup failed");
        RemoteAppearance.LoadCache("test-missing-" + Guid.NewGuid());
        if (RemoteAppearance.Get("title", "fallback") != "fallback") throw new Exception("Offline defaults failed");
        string testFile = Path.GetTempFileName();
        try {
            File.WriteAllText(testFile, "valid release bytes");
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(testFile))).Replace("-", "").ToLowerInvariant();
            if (!AutoUpdater.ValidateDownload(testFile, hash)) throw new Exception("Valid download rejected");
            File.AppendAllText(testFile, "corrupted");
            if (AutoUpdater.ValidateDownload(testFile, hash)) throw new Exception("Corrupted download accepted");
            if (AutoUpdater.ValidateDownload(testFile, null) || AutoUpdater.ValidateDownload(testFile, "invalid")) throw new Exception("Invalid hash accepted");
        } finally { File.Delete(testFile); }
        string slideJson = "[{\"id\":\"a\",\"image\":\"data:image/png;base64,AA==\",\"title\":\"Aviso\",\"seconds\":10}]";
        if (RemoteAppearance.ValidateSlides(slideJson).Count != 1) throw new Exception("Valid carousel rejected");
        string cloudImage = "https://res.cloudinary.com/donpjw2ed/image/upload/v123/checklist/media/photo.jpg";
        if (RemoteAppearance.ValidateSlides(slideJson.Replace("data:image/png;base64,AA==", cloudImage)).Count != 1) throw new Exception("Cloudinary rejected");
        if (MediaCache.IsAllowed(cloudImage.Replace("donpjw2ed", "other"), "image") || MediaCache.IsAllowed(cloudImage + "?redirect=1", "image")) throw new Exception("Untrusted media allowed");
        string video = "https://res.cloudinary.com/donpjw2ed/video/upload/vc_h264,ac_aac/v1/video.mp4";
        if (!MediaCache.IsAllowed(video, "video") || MediaCache.IsAllowed(video, "image")) throw new Exception("Video validation failed");
        if (MediaCache.FileFor(video) != MediaCache.FileFor(video) || !MediaCache.FileFor(video).EndsWith(".mp4")) throw new Exception("Cache key invalid");
        foreach (var invalidSlides in new[] { slideJson.Replace(":10", ":0"), slideJson.Replace(":10", ":61"), slideJson.Replace("data:image/png;base64,AA==", "file:///C:/private.png") }) {
            bool rejected = false;
            try { RemoteAppearance.ValidateSlides(invalidSlides); } catch { rejected = true; }
            if (!rejected) throw new Exception("Invalid carousel accepted");
        }
        var newer = typeof(AutoUpdater).GetMethod("IsNewerVersion", BindingFlags.Static | BindingFlags.NonPublic);
        if (!(bool)newer.Invoke(null, new object[] { "2.1.1", "2.0.2" }) || (bool)newer.Invoke(null, new object[] { "2.1.1", "2.1.1" })) throw new Exception("Version comparison failed");
        Console.WriteLine("PASS: SHA-256, tampering, version comparison; valid Unicode config, invalid fields, limits, lookup and missing-cache fallback.");
        return 0;
    }
}
