using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Text;
using System.IO;

// 개선된 MenuItemInfo 클래스 - 세분화된 메뉴 타입 분류
    public class MenuItemInfo
    {
        public string MenuPath { get; }
        public string DisplayName { get; }
        public string Shortcut { get; }
        public MenuType MenuType { get; }

        public MenuItemInfo(string menuPath, Assembly assembly)
        {
            MenuPath = menuPath;
            
            var (cleanPath, shortcut) = ExtractPathAndShortcut(menuPath);
            DisplayName = cleanPath.Replace("/", " ▸ ");
            Shortcut = ConvertShortcutToReadable(shortcut);
            
            MenuType = DetermineMenuType(assembly);
        }

        // 정확한 메뉴 타입 분류 로직
        private MenuType DetermineMenuType(Assembly assembly)
        {
            string assemblyName = assembly.GetName().Name;
            string assemblyLocation = assembly.Location;
            
            // Unity 내장 어셈블리
            if (IsUnityAssembly(assemblyName))
            {
                return MenuType.Unity;
            }
            
            // 플러그인 판별 (Plugins 폴더 또는 PackageManager 패키지)
            if (IsPluginAssembly(assemblyName, assemblyLocation))
            {
                return MenuType.Plugin;
            }
            
            // 에셋 스토어 에셋 판별 (일반적인 패턴들)
            if (IsAssetAssembly(assemblyName, assemblyLocation))
            {
                return MenuType.Asset;
            }
            
            // 나머지는 개발자 코드
            return MenuType.Developer;
        }

        private bool IsUnityAssembly(string assemblyName)
        {
            return assemblyName.StartsWith("Unity") ||
                   assemblyName.StartsWith("UnityEngine") ||
                   assemblyName.StartsWith("UnityEditor") ||
                   assemblyName.Contains("Unity.") ||
                   assemblyName.Contains("com.unity.");
        }

        private bool IsPluginAssembly(string assemblyName, string assemblyLocation)
        {
            // PackageManager 패키지들 (com.company.package 패턴)
            if (assemblyName.StartsWith("com.") || assemblyName.StartsWith("unity.") || 
                assemblyName.StartsWith("Unity.") && !IsUnityAssembly(assemblyName))
            {
                return true;
            }
            
            // 일반적인 플러그인/도구 이름 패턴들
            if (assemblyName.Contains("Plugin") || assemblyName.Contains("Tool") || 
                assemblyName.Contains("Editor") && !assemblyName.StartsWith("Assembly-CSharp") ||
                assemblyName.Contains("Runtime") && !assemblyName.StartsWith("Assembly-CSharp"))
            {
                return true;
            }
            
            // 어셈블리 정의 파일(.asmdef)을 사용하는 패키지들의 일반적인 패턴
            if (assemblyName.Contains(".") && !assemblyName.StartsWith("Assembly-CSharp") && 
                !IsUnityAssembly(assemblyName))
            {
                return true;
            }
            
            // 유명한 에셋/플러그인들의 특정 패턴
            string[] knownPluginPatterns = {
                "DOTween", "Odin", "ProBuilder", "TextMesh", "Cinemachine", 
                "Timeline", "Addressable", "Analytics", "XR", "VR", "AR",
                "PostProcessing", "URP", "HDRP", "Rider", "VisualStudio",
                "JetBrains", "GitHub", "Collab", "Cloud", "Services"
            };
            
            foreach (string pattern in knownPluginPatterns)
            {
                if (assemblyName.Contains(pattern))
                {
                    return true;
                }
            }
            
            return false;
        }

        private bool IsAssetAssembly(string assemblyName, string assemblyLocation)
        {
            // firstpass 어셈블리들만 에셋으로 분류 (Plugins 폴더의 써드파티 에셋들)
            return assemblyName == "Assembly-CSharp-Editor-firstpass" ||
                   assemblyName == "Assembly-CSharp-firstpass";
        }

        private (string cleanPath, string shortcut) ExtractPathAndShortcut(string menuPath)
        {
            int spaceIndex = menuPath.IndexOf(' ');
            
            while (spaceIndex > 0)
            {
                if (spaceIndex + 1 < menuPath.Length)
                {
                    char nextChar = menuPath[spaceIndex + 1];
                    if (nextChar == '%' || nextChar == '#' || nextChar == '&' || 
                        (nextChar == '_' && spaceIndex + 2 < menuPath.Length && char.IsDigit(menuPath[spaceIndex + 2])))
                    {
                        string cleanPath = menuPath.Substring(0, spaceIndex);
                        string shortcut = menuPath.Substring(spaceIndex + 1);
                        return (cleanPath, shortcut);
                    }
                }
                spaceIndex = menuPath.IndexOf(' ', spaceIndex + 1);
            }
            
            return (menuPath, string.Empty);
        }

        private string ConvertShortcutToReadable(string shortcut)
        {
            if (string.IsNullOrEmpty(shortcut))
                return string.Empty;

            var result = new StringBuilder();
            bool hasModifiers = false;

            if (shortcut.Contains("&"))
            {
                result.Append("Alt");
                hasModifiers = true;
            }
            
            if (shortcut.Contains("#"))
            {
                if (hasModifiers) result.Append(" + ");
                result.Append("Shift");
                hasModifiers = true;
            }
            
            if (shortcut.Contains("%"))
            {
                if (hasModifiers) result.Append(" + ");
                result.Append("Ctrl");
                hasModifiers = true;
            }

            string key = ExtractKey(shortcut);
            if (!string.IsNullOrEmpty(key))
            {
                if (hasModifiers) result.Append(" + ");
                result.Append(key);
            }

            return result.ToString();
        }

        private string ExtractKey(string shortcut)
        {
            string cleanShortcut = shortcut.Replace("&", "").Replace("#", "").Replace("%", "");
            
            if (cleanShortcut.StartsWith("_"))
            {
                string remaining = cleanShortcut.Substring(1);
                if (remaining.Length == 1 && char.IsDigit(remaining[0]))
                {
                    return remaining;
                }
                return remaining.ToUpper();
            }
            
            if (cleanShortcut.Length == 1)
            {
                return cleanShortcut.ToUpper();
            }
            
            return cleanShortcut;
        }
    }