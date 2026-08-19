using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckTracks.Systems
{
    /// <summary>
    /// 프로필 묶음을 파일로 옮기는 방법.
    ///
    /// <b>왜 배열을 그대로 안 쓰는가.</b>
    /// Unity의 <c>JsonUtility</c>는 맨 위 객체는 그냥 직렬화하지만, 그 <b>안에 든</b>
    /// 사용자 정의 클래스는 자기가 아는 타입일 때만 직렬화합니다. 런타임에 올라온 모드
    /// 어셈블리의 타입은 거기에 들어 있지 않아서, <c>[Serializable]</c>을 붙여 놨어도
    /// 조용히 버려집니다.
    ///
    /// 실측이 정확히 그랬습니다 — 저장 파일 다섯 개가 전부 이랬습니다:
    /// <code>
    /// 저장 직렬화(오라): 2자, 항목 7개, 앞부분={}
    /// 저장 직렬화(총구): 20자, 항목 7개, 앞부분={ "version": 0 }
    /// </code>
    /// 프로필은 7개가 멀쩡히 있는데 배열 필드만 통째로 빠졌고, 정수(<c>version</c>)는
    /// 남았습니다. 불러오기 성공 로그는 지난 실행 기록까지 뒤져도 <b>한 건도</b> 없었습니다.
    /// 저장이 되는 줄 알았을 뿐 처음부터 안 되고 있었습니다.
    ///
    /// <b>해결.</b> 프로필 하나하나를 <b>맨 위 객체로</b> 직렬화해서 문자열로 만들고,
    /// 파일에는 그 <b>문자열 배열</b>만 담습니다. 문자열은 Unity가 항상 아는 타입이라
    /// 배열째로 안전하게 오갑니다. 설정 공유 기능(<c>WAURA1:</c>)이 프로필 하나를
    /// 맨 위로 직렬화해서 잘 동작하고 있었다는 것이 이 방법이 통한다는 증거입니다.
    /// </summary>
    internal static class ProfileJson
    {
        /// <summary>프로필 배열 → JSON 문자열 배열.</summary>
        public static string[] Pack<T>(T[]? items) where T : class
        {
            if (items == null || items.Length == 0)
                return Array.Empty<string>();

            var result = new List<string>(items.Length);

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                try
                {
                    result.Add(JsonUtility.ToJson(item));
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[DuckTracks] 프로필 하나를 담지 못했습니다: {ex.Message}");
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// JSON 문자열 배열 → 프로필 배열.
        ///
        /// 하나가 깨져 있어도 나머지는 살립니다 — 파일 한 줄 때문에 설정 전체를 잃는 것이
        /// 제일 나쁩니다.
        /// </summary>
        public static T[] Unpack<T>(string[]? raw) where T : class
        {
            if (raw == null || raw.Length == 0)
                return Array.Empty<T>();

            var result = new List<T>(raw.Length);

            foreach (string json in raw)
            {
                if (string.IsNullOrEmpty(json))
                    continue;

                try
                {
                    var item = JsonUtility.FromJson<T>(json);
                    if (item != null)
                        result.Add(item);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[DuckTracks] 프로필 하나를 읽지 못했습니다: {ex.Message}");
                }
            }

            return result.ToArray();
        }

        /// <summary>프로필 하나를 문자열로 (전용 설정처럼 개별 보관이 필요한 곳에서).</summary>
        public static string One<T>(T? item) where T : class
        {
            if (item == null)
                return "";

            try
            {
                return JsonUtility.ToJson(item);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>문자열 하나를 프로필로. 비어 있거나 깨졌으면 null.</summary>
        public static T? OneFrom<T>(string? json) where T : class
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(json!);
            }
            catch
            {
                return null;
            }
        }
    }
}
