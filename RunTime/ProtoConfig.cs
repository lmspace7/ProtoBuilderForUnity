using System;
using System.Collections.Generic;
using UnityEngine;

namespace LM.ProtoBuilder
{
    /// <summary>
    /// 프로토 설정을 보관하는 스크립터블 오브젝트
    /// - 서버의 .proto 원본 경로와 클라이언트 대상 경로를 항목 단위로 관리
    /// - 항목은 리스트로 관리되며, 각 항목은 이름/원본/대상 경로 정보를 포함
    /// </summary>
    [CreateAssetMenu(menuName = "설정/프로토 설정", fileName = "ProtoConfig")]
    public class ProtoConfig : ScriptableObject
    {
        [SerializeField] private List<Entry> _entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>
        /// 프로토 항목 구조체
        /// - Name: 구분용 이름(예: 인증서버, 게임서버)
        /// - SourcePath: 서버 .proto 원본 폴더 경로
        /// - DestinationPath: 클라이언트 .proto 대상 폴더 경로(ex - Assets/Protos)
        /// - OutputCsPath: 생성되는 C# 산출물 폴더 경로(ex - Assets/Scripts/Generated)
        /// </summary>
        [Serializable]
        public struct Entry
        {
            [SerializeField] private string _name;
            [SerializeField] private string _sourcePath;
            [SerializeField] private string _destinationPath;
            [SerializeField] private string _outputCsPath;

            public string Name => _name;
            public string SourcePath => _sourcePath;
            public string DestinationPath => _destinationPath;
            public string OutputCsPath => _outputCsPath;
        }
    }
}


