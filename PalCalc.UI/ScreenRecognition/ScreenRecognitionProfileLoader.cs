using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;

namespace PalCalc.UI.ScreenRecognition
{
    public static class ScreenRecognitionProfileLoader
    {
        public static ScreenRecognitionProfile LoadKorean1080pBorderless()
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "ScreenRecognition",
                "Profiles",
                "ko-1920x1080-borderless.json"
            );

            string json;
            if (File.Exists(path))
            {
                json = File.ReadAllText(path);
            }
            else
            {
                const string resourceName =
                    "PalCalc.UI.ScreenRecognition.Profiles.ko-1920x1080-borderless.json";
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                if (stream == null)
                    throw new FileNotFoundException("화면 인식 프로필을 찾을 수 없습니다.", path);

                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }

            var profile = JsonConvert.DeserializeObject<ScreenRecognitionProfile>(json);
            return profile ?? throw new InvalidDataException("화면 인식 프로필을 읽지 못했습니다.");
        }
    }
}
