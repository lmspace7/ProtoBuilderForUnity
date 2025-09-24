using UnityEditor;
using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using LM.ProtoBuilder;

namespace LM.ProtoBuilder.Editor
{
    public class ProtobufBuilder
    {
        [MenuItem("에디터툴/ProtoBuilder/Create Default ProtoConfig")]
        public static void Create_ProtoConfig()
        {
            string assetDir = "Assets/Scripts/Config";
            string assetPath = Path.Combine(assetDir, "ProtoConfig.asset");

            // 이미 존재하면 덮어쓰기 방지: 기존 에셋 선택만 수행
            if (File.Exists(assetPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = existing;
                EditorUtility.DisplayDialog("프로토 설정", "기존 ProtoConfig 에셋이 이미 존재합니다. 새로 생성하지 않았습니다.", "확인");
                return;
            }

            if (Directory.Exists(assetDir) == false)
                Directory.CreateDirectory(assetDir);

            var config = ScriptableObject.CreateInstance("ProtoConfig");
            if (config == null)
            {
                EditorUtility.DisplayDialog("프로토 설정", "ProtoConfig 타입을 찾을 수 없습니다. 해당 스크립트가 존재하는지 확인하세요.", "확인");
                return;
            }

            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            EditorUtility.DisplayDialog("프로토 설정", "기본 ProtoConfig 에셋을 생성했습니다.", "확인");
        }

        [MenuItem("에디터툴/ProtoBuilder/Generate Client Protos C# %l")]
        public static void GenerateAllProtos()
        {
            SyncProtos();
            GenerateProtos();
        }

        private static void SyncProtos()
        {
            ProtoConfig configData = FindProtoConfig();
            if (configData == null)
            {
                EditorUtility.DisplayDialog("프로토 동기화", "ProtoConfig 에셋을 찾을 수 없습니다.", "확인");
                return;
            }
            if (configData.Entries == null || configData.Entries.Count <= 0)
            {
                EditorUtility.DisplayDialog("프로토 동기화", "ProtoConfig에 항목이 없습니다.", "확인");
                return;
            }

            int totalCopied = 0;
            foreach (var entry in configData.Entries)
                totalCopied += SyncProto(entry.SourcePath, entry.DestinationPath);

            EditorUtility.DisplayDialog("프로토 동기화 완료", $"총 복사된 파일: {totalCopied}개", "확인");
        }

        private static int SyncProto(string sourceDir, string destDir)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            // 절대 경로 우선, 상대 경로면 프로젝트 루트 기준으로 해석
            string serverProtoDir = Path.IsPathRooted(sourceDir) ? sourceDir : Path.GetFullPath(Path.Combine(projectRoot, sourceDir));
            string clientProtoDir = Path.IsPathRooted(destDir) ? destDir : Path.GetFullPath(Path.Combine(projectRoot, destDir));

            if (Directory.Exists(serverProtoDir) == false)
            {
                EditorUtility.DisplayDialog("프로토 동기화", "서버 프로토 경로를 찾을 수 없습니다:\n" + serverProtoDir, "확인");
                return 0;
            }

            Directory.CreateDirectory(clientProtoDir);

            // 상대경로 계산, 덮어쓰기 복사
            // Path.DirectorySeparatorChar - 운영체제에 속하는 구분자 (윈도우: '\')
            int copiedCount = 0;
            string separator = Path.DirectorySeparatorChar.ToString();
            string uriStr = serverProtoDir.EndsWith(separator) ? serverProtoDir : serverProtoDir + separator;
            Uri baseUri = new Uri(uriStr);
            string[] sourceFiles = Directory.GetFiles(serverProtoDir, "*.proto", SearchOption.AllDirectories);
            foreach (string sourcePath in sourceFiles)
            {
                Uri fileUri = new Uri(sourcePath);
                string relativePath = Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
                string destinationPath = Path.Combine(clientProtoDir, relativePath);

                string destinationDir = Path.GetDirectoryName(destinationPath);
                if (string.IsNullOrEmpty(destinationDir) == false)
                    Directory.CreateDirectory(destinationDir);

                File.Copy(sourcePath, destinationPath, true);
                copiedCount++;
            }

            AssetDatabase.Refresh();
            return copiedCount;
        }

        private static void GenerateProtos()
        {
            ProtoConfig configData = FindProtoConfig();
            if (configData == null)
            {
                EditorUtility.DisplayDialog("gRPC 생성", "ProtoConfig 에셋을 찾을 수 없습니다.", "확인");
                return;
            }
            if (configData.Entries == null || configData.Entries.Count <= 0)
            {
                EditorUtility.DisplayDialog("gRPC 생성", "ProtoConfig에 항목이 없습니다.", "확인");
                return;
            }

            foreach (var entry in configData.Entries)
                GenerateProto(entry.DestinationPath, entry.OutputCsPath);
        }

        private static ProtoConfig FindProtoConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:ProtoConfig");
            if (guids == null || guids.Length == 0)
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var config = AssetDatabase.LoadAssetAtPath<ProtoConfig>(path);
            return config;
        }

        /// <summary>
        /// Grpc.Tools 버전을 자동으로 탐색하여 문자열로 반환.
        /// - 우선순위:
        ///   1) 환경변수 GRPC_TOOLS_VERSION (존재 시 그대로 사용)
        ///   2) Packages/Grpc.Tools.* 디렉터리의 가장 높은 SemVer를 자동 선택
        /// - 반환 예: "2.72.0". 찾지 못하면 빈 문자열 반환
        /// </summary>
        private static string ResolveGrpcToolsVersion(string projectRoot)
        {
            string envVersion = Environment.GetEnvironmentVariable("GRPC_TOOLS_VERSION");
            if (string.IsNullOrEmpty(envVersion) == false)
            {
                return envVersion;
            }

            // 검색 루트: Unity 프로젝트 루트/상위 리포 루트 모두 확인
            string[] candidatePackageRoots = new string[]
            {
            Path.Combine(projectRoot, "Packages"),
            Path.GetFullPath(Path.Combine(projectRoot, "..", "Packages"))
            };

            Version best = null;
            foreach (string packagesDir in candidatePackageRoots)
            {
                if (Directory.Exists(packagesDir) == false)
                {
                    continue;
                }
                string[] dirs = Directory.GetDirectories(packagesDir, "Grpc.Tools.*", SearchOption.TopDirectoryOnly);
                if (dirs == null || dirs.Length == 0)
                {
                    continue;
                }
                foreach (string dir in dirs)
                {
                    string name = Path.GetFileName(dir);
                    string versionPart = name.Replace("Grpc.Tools.", string.Empty);
                    if (Version.TryParse(versionPart, out Version v) == true)
                    {
                        if (best == null || v > best)
                        {
                            best = v;
                        }
                    }
                }
            }

            if (best == null)
            {
                return string.Empty;
            }

            return best.ToString();
        }

        private static void GenerateProto(string protoDirInput, string outputDirInput)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            // 절대 경로 우선, 상대 경로면 프로젝트 루트 기준으로 해석
            string protoDir = Path.IsPathRooted(protoDirInput) ? protoDirInput : Path.GetFullPath(Path.Combine(projectRoot, protoDirInput));
            string outputDir = Path.IsPathRooted(outputDirInput) ? outputDirInput : Path.GetFullPath(Path.Combine(projectRoot, string.IsNullOrEmpty(outputDirInput) ? Path.Combine("Assets", "Scripts", "Generated") : outputDirInput));
            string version = ResolveGrpcToolsVersion(projectRoot);
            if (string.IsNullOrEmpty(version) == true)
            {
                EditorUtility.DisplayDialog("gRPC 생성", "Grpc.Tools 버전을 찾을 수 없습니다. Packages 폴더를 확인하세요.", "확인");
                return;
            }
            // 패키지 루트 결정: 프로젝트 루트/상위 루트 중 실제 버전 폴더가 존재하는 경로 선택
            string candidateRootA = Path.Combine(projectRoot, "Packages");
            string candidateRootB = Path.GetFullPath(Path.Combine(projectRoot, "..", "Packages"));
            string chosenPackagesRoot = string.Empty;
            string dirA = Path.Combine(candidateRootA, $"Grpc.Tools.{version}");
            string dirB = Path.Combine(candidateRootB, $"Grpc.Tools.{version}");
            if (Directory.Exists(dirA) == true)
            {
                chosenPackagesRoot = candidateRootA;
            }
            else if (Directory.Exists(dirB) == true)
            {
                chosenPackagesRoot = candidateRootB;
            }
            else
            {
                EditorUtility.DisplayDialog("gRPC 생성", "Grpc.Tools 패키지 폴더를 찾을 수 없습니다. Packages 경로를 확인하세요.", "확인");
                return;
            }

            string toolsBaseDir = Path.Combine(chosenPackagesRoot, $"Grpc.Tools.{version}", "tools");
            string archDirName = Environment.Is64BitProcess ? "windows_x64" : "windows_x86";
            string toolsDir = Path.Combine(toolsBaseDir, archDirName);
            string toolsIncludeDir = Path.Combine(toolsBaseDir, "include");
            // NuGet 전역 캐시의 well-known types 경로 (build/native/include)
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string nugetNativeIncludeDir = Path.Combine(userProfile, ".nuget", "packages", "grpc.tools", version, "build", "native", "include");
            string protocPath = Path.Combine(toolsDir, "protoc.exe");
            string grpcPluginPath = Path.Combine(toolsDir, "grpc_csharp_plugin.exe");

            if (Directory.Exists(protoDir) == false)
            {
                EditorUtility.DisplayDialog("gRPC 생성", "프로토 입력 폴더를 찾을 수 없습니다:\n" + protoDir, "확인");
                return;
            }

            if (File.Exists(protocPath) == false)
            {
                EditorUtility.DisplayDialog("gRPC 생성", "protoc.exe 경로를 찾을 수 없습니다:\n" + protocPath, "확인");
                return;
            }

            if (File.Exists(grpcPluginPath) == false)
            {
                EditorUtility.DisplayDialog("gRPC 생성", "grpc_csharp_plugin.exe 경로를 찾을 수 없습니다:\n" + grpcPluginPath, "확인");
                return;
            }

            bool hasToolsInclude = Directory.Exists(toolsIncludeDir);
            bool hasNugetInclude = Directory.Exists(nugetNativeIncludeDir);
            if (hasToolsInclude == false && hasNugetInclude == false)
            {
                EditorUtility.DisplayDialog("gRPC 생성", "Protobuf include 경로를 찾을 수 없습니다.\n시도한 경로:\n" + toolsIncludeDir + "\n" + nugetNativeIncludeDir, "확인");
                return;
            }

            Directory.CreateDirectory(outputDir);

            // 대상 proto 파일 수집
            string[] protoFiles = Directory.GetFiles(protoDir, "*.proto", SearchOption.AllDirectories);
            if (protoFiles == null || protoFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("gRPC 생성", "변환할 .proto 파일이 없습니다.", "확인");
                return;
            }

            // protoc 인자 구성
            StringBuilder argumentsBuilder = new StringBuilder();
            argumentsBuilder.Append(" -I=\"").Append(protoDir).Append("\"");
            if (hasToolsInclude == true)
            {
                argumentsBuilder.Append(" -I=\"").Append(toolsIncludeDir).Append("\"");
            }
            if (hasNugetInclude == true)
            {
                argumentsBuilder.Append(" -I=\"").Append(nugetNativeIncludeDir).Append("\"");
            }
            argumentsBuilder.Append(" --csharp_out=\"").Append(outputDir).Append("\"");
            argumentsBuilder.Append(" --grpc_out=\"").Append(outputDir).Append("\"");
            argumentsBuilder.Append(" --plugin=protoc-gen-grpc=\"").Append(grpcPluginPath).Append("\"");
            foreach (string file in protoFiles)
            {
                argumentsBuilder.Append(" \"").Append(file).Append("\"");
            }

            // 외부 프로세스 실행 정보
            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.FileName = protocPath;
            processInfo.Arguments = argumentsBuilder.ToString();
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            processInfo.StandardOutputEncoding = Encoding.UTF8;
            processInfo.StandardErrorEncoding = Encoding.UTF8;
            processInfo.WorkingDirectory = projectRoot;

            // 프로세스 실행 및 결과 처리
            using (Process process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                int exitCode = process.ExitCode;

                if (string.IsNullOrEmpty(stdout) == false)
                {
                    UnityEngine.Debug.Log(stdout);
                }

                if (exitCode != 0 || string.IsNullOrEmpty(stderr) == false)
                {
                    UnityEngine.Debug.LogError(stderr);
                    EditorUtility.DisplayDialog("gRPC 생성 실패", "코드 생성 중 오류가 발생했습니다. 콘솔 로그를 확인하세요.", "확인");
                    return;
                }
            }

            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("gRPC C# generation complete.");
        }
    }
}