using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Text;
using System.IO;

public class MenuItemFinderWindow : EditorWindow
{
    private const int MIN_WINDOW_WIDTH = 400;
    private const int MIN_WINDOW_HEIGHT = 350; // 높이 증가
    private const string SEARCH_FIELD_NAME = "MenuSearchField";
    
    private string searchQuery = "";
    private Vector2 scrollPosition;
    private List<MenuItemInfo> filteredMenuItems = new List<MenuItemInfo>();
    private List<MenuItemInfo> allMenuItems = new List<MenuItemInfo>();
    private int selectedIndex = 0;
    private bool shouldFocusSearchField = true;
    
    // 세분화된 필터 옵션들
    private bool showUnityMenus = true;
    private bool showPluginMenus = true;
    private bool showAssetMenus = true;
    private bool showDeveloperMenus = true;
    
    private GUIStyle selectedButtonStyle;
    private GUIStyle normalButtonStyle;
    private GUIStyle shortcutStyle;

    [MenuItem("Window/Menu Item Finder &#m")]
    public static void ShowWindow()
    {
        var window = GetWindow<MenuItemFinderWindow>("Menu Item Finder");
        window.minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
        window.Focus();
    }

    private void OnEnable()
    {
        LoadAllMenuItems();
        FilterMenuItems();
    }

    private void OnGUI()
    {
        InitializeStylesIfNeeded();
        DrawSearchField();
        DrawFilterOptions();
        EditorGUILayout.Space(5);
        HandleKeyboardInput();
        DrawMenuItemList();
    }

    // 개선된 필터 옵션 UI - 세로 배치로 겹침 방지
    private void DrawFilterOptions()
    {
        EditorGUILayout.LabelField("Filter Options:", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 첫 번째 행
        EditorGUILayout.BeginHorizontal();
        bool newShowUnity = EditorGUILayout.ToggleLeft("Unity Menus", showUnityMenus, GUILayout.Width(100));
        bool newShowPlugin = EditorGUILayout.ToggleLeft("Plugin Menus", showPluginMenus, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        
        // 두 번째 행
        EditorGUILayout.BeginHorizontal();
        bool newShowAsset = EditorGUILayout.ToggleLeft("Asset Menus", showAssetMenus, GUILayout.Width(100));
        bool newShowDeveloper = EditorGUILayout.ToggleLeft("Project Menus", showDeveloperMenus, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        // 필터 옵션이 변경되었는지 확인
        if (newShowUnity != showUnityMenus || newShowPlugin != showPluginMenus || 
            newShowAsset != showAssetMenus || newShowDeveloper != showDeveloperMenus)
        {
            showUnityMenus = newShowUnity;
            showPluginMenus = newShowPlugin;
            showAssetMenus = newShowAsset;
            showDeveloperMenus = newShowDeveloper;
            FilterMenuItems();
            ResetSelection();
        }
    }

    private void InitializeStylesIfNeeded()
    {
        if (normalButtonStyle == null)
        {
            normalButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 4, 4)
            };
            
            selectedButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 4, 4)
            };
            
            selectedButtonStyle.normal.background = CreateColorTexture(new Color(0.2f, 0.4f, 0.8f, 0.8f));
            selectedButtonStyle.hover.background = CreateColorTexture(new Color(0.2f, 0.4f, 0.8f, 0.8f));
            selectedButtonStyle.active.background = CreateColorTexture(new Color(0.2f, 0.4f, 0.8f, 0.8f));
            selectedButtonStyle.normal.textColor = Color.white;
            selectedButtonStyle.hover.textColor = Color.white;
            selectedButtonStyle.active.textColor = Color.white;

            shortcutStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.gray }
            };
        }
    }

    private Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void DrawSearchField()
    {
        GUI.SetNextControlName(SEARCH_FIELD_NAME);
        string newSearchQuery = EditorGUILayout.TextField("Search Menu Items:", searchQuery);
        
        if (newSearchQuery != searchQuery)
        {
            searchQuery = newSearchQuery;
            FilterMenuItems();
            ResetSelection();
        }

        if (shouldFocusSearchField)
        {
            EditorGUI.FocusTextInControl(SEARCH_FIELD_NAME);
            shouldFocusSearchField = false;
        }
    }

    private void HandleKeyboardInput()
    {
        Event e = Event.current;
        
        if (e.type == EventType.KeyDown)
        {
            bool isSearchFieldFocused = GUI.GetNameOfFocusedControl() == SEARCH_FIELD_NAME;
            
            switch (e.keyCode)
            {
                case KeyCode.UpArrow:
                    if (filteredMenuItems.Count > 0)
                    {
                        if (isSearchFieldFocused)
                        {
                            GUI.FocusControl(null);
                            selectedIndex = filteredMenuItems.Count - 1;
                        }
                        else
                        {
                            selectedIndex = (selectedIndex - 1 + filteredMenuItems.Count) % filteredMenuItems.Count;
                        }
                        EnsureSelectedItemVisible();
                        e.Use();
                        Repaint();
                    }
                    break;
    
                case KeyCode.DownArrow:
                    if (filteredMenuItems.Count > 0)
                    {
                        if (isSearchFieldFocused)
                        {
                            GUI.FocusControl(null);
                            selectedIndex = 0;
                        }
                        else
                        {
                            selectedIndex = (selectedIndex + 1) % filteredMenuItems.Count;
                        }
                        EnsureSelectedItemVisible();
                        e.Use();
                        Repaint();
                    }
                    break;
                    
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (IsValidSelection())
                    {
                        ExecuteSelectedMenuItem();
                        e.Use();
                    }
                    break;
                    
                case KeyCode.Escape:
                    Close();
                    e.Use();
                    break;
            }
        }
    }

    private void DrawMenuItemList()
    {
        if (filteredMenuItems.Count == 0)
        {
            EditorGUILayout.HelpBox("No menu items found matching your search and filter criteria.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        for (int i = 0; i < filteredMenuItems.Count; i++)
        {
            DrawMenuItemButton(i, filteredMenuItems[i]);
        }
        
        EditorGUILayout.EndScrollView();
        
        DrawStatusBar();
    }

    private void DrawMenuItemButton(int index, MenuItemInfo menuItem)
    {
        bool isSelected = index == selectedIndex;
        GUIStyle style = isSelected ? selectedButtonStyle : normalButtonStyle;
        
        // 메뉴 타입에 따른 아이콘 추가
        string icon = GetMenuTypeIcon(menuItem.MenuType);
        string displayText = isSelected ? $"▶ {icon} {menuItem.DisplayName}" : $"   {icon} {menuItem.DisplayName}";
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button(displayText, style, GUILayout.Height(22), GUILayout.ExpandWidth(true)))
        {
            selectedIndex = index;
            ExecuteSelectedMenuItem();
        }
        
        if (!string.IsNullOrEmpty(menuItem.Shortcut))
        {
            EditorGUILayout.LabelField(menuItem.Shortcut, shortcutStyle, GUILayout.Width(120));
        }
        
        EditorGUILayout.EndHorizontal();
        
        HandleMouseSelection(index);
    }

    private string GetMenuTypeIcon(MenuType menuType)
    {
        switch (menuType)
        {
            case MenuType.Unity: return "🔧";
            case MenuType.Plugin: return "🔌";
            case MenuType.Asset: return "📦";
            case MenuType.Developer: return "💻";
            default: return "❓";
        }
    }

    private void DrawStatusBar()
    {
        if (filteredMenuItems.Count > 0)
        {
            var counts = GetMenuTypeCounts();
            string statusText = $"{selectedIndex + 1} / {filteredMenuItems.Count} " +
                              $"(Unity: {counts.unity}, Plugin: {counts.plugin}, Asset: {counts.asset}, Project: {counts.developer})";
            EditorGUILayout.LabelField(statusText, EditorStyles.centeredGreyMiniLabel);
        }
    }

    private (int unity, int plugin, int asset, int developer) GetMenuTypeCounts()
    {
        int unity = filteredMenuItems.Count(item => item.MenuType == MenuType.Unity);
        int plugin = filteredMenuItems.Count(item => item.MenuType == MenuType.Plugin);
        int asset = filteredMenuItems.Count(item => item.MenuType == MenuType.Asset);
        int developer = filteredMenuItems.Count(item => item.MenuType == MenuType.Developer);
        
        return (unity, plugin, asset, developer);
    }

    private void HandleMouseSelection(int index)
    {
        Rect buttonRect = GUILayoutUtility.GetLastRect();
        Event currentEvent = Event.current;
        
        if (currentEvent.type == EventType.MouseDown && 
            buttonRect.Contains(currentEvent.mousePosition))
        {
            selectedIndex = index;
            GUI.FocusControl(null);
            Repaint();
            currentEvent.Use();
        }
    }

    private void EnsureSelectedItemVisible()
    {
        if (filteredMenuItems.Count == 0) return;
    
        float itemHeight = 24f;
        float selectedItemTop = selectedIndex * itemHeight;
        float selectedItemBottom = selectedItemTop + itemHeight;
    
        // 필터 옵션 UI가 추가되어 스크롤뷰 높이 재조정
        float scrollViewTop = scrollPosition.y;
        float scrollViewHeight = position.height - 120f; // 검색 필드, 필터 옵션, 상태바를 제외한 높이
        float scrollViewBottom = scrollViewTop + scrollViewHeight;
    
        if (selectedItemTop < scrollViewTop)
        {
            scrollPosition.y = selectedItemTop;
        }
        else if (selectedItemBottom > scrollViewBottom)
        {
            scrollPosition.y = selectedItemBottom - scrollViewHeight;
        }
    
        Repaint();
    }

    private void ExecuteSelectedMenuItem()
    {
        if (IsValidSelection())
        {
            MenuItemInfo selectedItem = filteredMenuItems[selectedIndex];
            EditorApplication.ExecuteMenuItem(selectedItem.MenuPath);
            Close();
        }
    }

    private void LoadAllMenuItems()
    {
        allMenuItems.Clear();
        
        var menuItemMethods = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => GetMenuItemMethods(assembly))
            .Where(method => method != null);

        foreach (var method in menuItemMethods)
        {
            var menuItemInfo = CreateMenuItemInfo(method);
            if (menuItemInfo != null)
            {
                allMenuItems.Add(menuItemInfo);
            }
        }

        allMenuItems = allMenuItems.OrderBy(item => item.MenuPath).ToList();
    }

    private IEnumerable<MethodInfo> GetMenuItemMethods(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method => method.GetCustomAttributes(typeof(MenuItem), false).Length > 0);
        }
        catch
        {
            return Enumerable.Empty<MethodInfo>();
        }
    }

    private MenuItemInfo CreateMenuItemInfo(MethodInfo method)
    {
        try
        {
            var menuItemAttribute = method.GetCustomAttributes(typeof(MenuItem), false)
                .FirstOrDefault() as MenuItem;
            
            if (menuItemAttribute != null)
            {
                return new MenuItemInfo(menuItemAttribute.menuItem, method.DeclaringType.Assembly);
            }
        }
        catch
        {
            // Skip problematic menu items
        }
        
        return null;
    }

    // 개선된 FilterMenuItems - 세분화된 필터링 적용
    private void FilterMenuItems()
    {
        var itemsToFilter = allMenuItems.Where(item =>
            (item.MenuType == MenuType.Unity && showUnityMenus) ||
            (item.MenuType == MenuType.Plugin && showPluginMenus) ||
            (item.MenuType == MenuType.Asset && showAssetMenus) ||
            (item.MenuType == MenuType.Developer && showDeveloperMenus)
        ).ToList();
        
        if (string.IsNullOrEmpty(searchQuery))
        {
            filteredMenuItems = new List<MenuItemInfo>(itemsToFilter);
        }
        else
        {
            string lowerQuery = searchQuery.ToLower();
            filteredMenuItems = itemsToFilter
                .Where(item => item.DisplayName.ToLower().Contains(lowerQuery) ||
                              item.MenuPath.ToLower().Contains(lowerQuery) ||
                              (!string.IsNullOrEmpty(item.Shortcut) && item.Shortcut.ToLower().Contains(lowerQuery)))
                .ToList();
        }
    }

    private void ResetSelection()
    {
        selectedIndex = filteredMenuItems.Count > 0 ? 0 : -1;
    }

    private bool IsValidSelection()
    {
        return selectedIndex >= 0 && selectedIndex < filteredMenuItems.Count;
    }

    // 메뉴 타입 열거형
    private enum MenuType
    {
        Unity,
        Plugin,
        Asset,
        Developer
    }

    // 개선된 MenuItemInfo 클래스 - 세분화된 메뉴 타입 분류
    private class MenuItemInfo
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
}