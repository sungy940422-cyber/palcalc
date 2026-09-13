using Newtonsoft.Json;
using System;
using System.IO;

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

            if (!File.Exists(path))
                throw new FileNotFoundException("화면 인식 프로필을 찾을 수 없습니다.", path);

            var profile = JsonConvert.DeserializeObject<ScreenRecognitionProfile>(File.ReadAllText(path));
            return profile ?? throw new InvalidDataException("화면 인식 프로필을 읽지 못했습니다.");
        }
    }
}
