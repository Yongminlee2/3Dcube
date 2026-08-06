using NUnit.Framework;
using UnityEngine;

namespace Cube.App.Tests
{
    /// 색 비교 도우미.
    ///
    /// 선형 색공간에서 Material.color는 넣을 때 sRGB->선형, 뺄 때 선형->sRGB로
    /// 바뀌므로 되돌아온 값이 원본과 아주 조금 다르다. NUnit의 AreEqual은
    /// Color.Equals(정확 비교)를 쓰기 때문에 눈에 같아 보여도 실패한다.
    public static class TestColors
    {
        const float Tolerance = 0.003f;

        public static void AssertSame(Color expected, Color actual, string message = "")
        {
            Assert.AreEqual(expected.r, actual.r, Tolerance, $"{message} (r) 기대 {expected} 실제 {actual}");
            Assert.AreEqual(expected.g, actual.g, Tolerance, $"{message} (g) 기대 {expected} 실제 {actual}");
            Assert.AreEqual(expected.b, actual.b, Tolerance, $"{message} (b) 기대 {expected} 실제 {actual}");
        }
    }
}
