using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// C# Multi File Generator (Fixed: Supports Attributes like [Serializable], comments, etc.)
/// </summary>
public class CSharpFileGenerator : EditorWindow
{
	private const string WINDOW_TITLE = "C# Multi File Generator";
	private const string MENU_PATH = "Tools/C# File Generator #&c";
	private static readonly Vector2 MIN_WINDOW_SIZE = new Vector2(980, 640);

	private string _lastChosenPath = "Assets";
	private DefaultAsset _defaultPathAsset;

	private bool _autoSplitOnSave = true;
	private bool _showSavePlan = true;

	[System.Serializable]
	private class FileEntry
	{
		public string displayName = "New Script";
		public string code = "";
		public string explicitFilename = "";

		public bool useCustomPath = false;
		public string customSavePath = "";
		public string stickyDefaultPath = "";
		public bool stickToDefault = true;

		public string lastStatus = "";
		public MessageType lastStatusType = MessageType.None;

		public string GetEffectivePath(string globalDefault)
		{
			if (useCustomPath && !string.IsNullOrWhiteSpace(customSavePath))
				return customSavePath;

			if (stickToDefault)
			{
				if (string.IsNullOrWhiteSpace(stickyDefaultPath))
					stickyDefaultPath = string.IsNullOrWhiteSpace(globalDefault) ? "Assets" : globalDefault;
				return stickyDefaultPath;
			}
			return string.IsNullOrWhiteSpace(globalDefault) ? "Assets" : globalDefault;
		}

		public void RebindTo(string newDefault)
		{
			stickToDefault = true;
			stickyDefaultPath = string.IsNullOrWhiteSpace(newDefault) ? "Assets" : newDefault;
			useCustomPath = false;
		}
	}

	private List<FileEntry> _entries = new List<FileEntry>();
	private int _selectedIndex = -1;

	private Vector2 _leftScroll;
	private Vector2 _codeScroll;
	private Vector2 _savePlanScroll;

	private StatusMessage _status = new StatusMessage();

	private enum OverwritePolicy { Ask, OverwriteAll, SkipAll, RenameIfExists }
	private OverwritePolicy _batchOverwritePolicy = OverwritePolicy.Ask;

	private struct AskMemory { public bool hasDecision, overwrite, applyToAll; }
	private AskMemory _askMemory;

	private static readonly HashSet<string> UNITY_BASE_CLASSES = new HashSet<string>
	{
		"MonoBehaviour", "MonoBehavior", "ScriptableObject", "EditorWindow",
		"Editor", "PropertyDrawer", "AssetPostprocessor", "ScriptableWizard",
		"PreprocessBuild", "PostProcessBuild"
	};

	[MenuItem(MENU_PATH)]
	public static void ShowWindow()
	{
		var w = GetWindow<CSharpFileGenerator>(WINDOW_TITLE);
		w.minSize = MIN_WINDOW_SIZE;
		w.InitializeIfNeeded();
	}

	private void InitializeIfNeeded()
	{
		if (_entries.Count == 0)
		{
			var e = new FileEntry();
			e.explicitFilename = e.displayName;
			_entries.Add(e);
			_selectedIndex = 0;
		}

		var sel = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => !string.IsNullOrEmpty(p));
		if (!string.IsNullOrEmpty(sel))
			_lastChosenPath = AssetDatabase.IsValidFolder(sel) ? sel : GetDirectoryFromPath(sel);

		_defaultPathAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_lastChosenPath);
	}

	private void OnEnable()
	{
		Selection.selectionChanged += OnSelectionChangedPickDefault;
		InitializeIfNeeded();
	}

	private void OnDisable()
	{
		Selection.selectionChanged -= OnSelectionChangedPickDefault;
	}

	private void OnSelectionChangedPickDefault()
	{
		var sel = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => !string.IsNullOrEmpty(p));
		if (string.IsNullOrEmpty(sel)) return;

		_lastChosenPath = AssetDatabase.IsValidFolder(sel) ? sel : GetDirectoryFromPath(sel);
		_defaultPathAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_lastChosenPath);

		if (_selectedIndex >= 0 && _selectedIndex < _entries.Count)
		{
			var selectedEntry = _entries[_selectedIndex];
			if (selectedEntry.stickToDefault && !selectedEntry.useCustomPath)
			{
				selectedEntry.stickyDefaultPath = _lastChosenPath;
			}
		}
		Repaint();
	}

	private void OnGUI()
	{
		DrawHeaderBar();
		EditorGUILayout.Space(2);

		EditorGUILayout.BeginHorizontal();
		DrawLeftEntryList();
		DrawRightEntryDetail();
		EditorGUILayout.EndHorizontal();

		DrawBottomBar();
		DrawSavePlanPanel();
		DrawStatusMessage();
	}

	private void DrawHeaderBar()
	{
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Auto Split on Save", GUILayout.Width(150));
		_autoSplitOnSave = EditorGUILayout.Toggle(_autoSplitOnSave, GUILayout.Width(24));

		GUILayout.Space(12);
		EditorGUILayout.LabelField("Overwrite Policy:", GUILayout.Width(120));
		_batchOverwritePolicy = (OverwritePolicy)EditorGUILayout.EnumPopup(_batchOverwritePolicy, GUILayout.Width(180));

		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		var folderIcon = EditorGUIUtility.IconContent("Folder Icon").image;
		GUILayout.Label(folderIcon, GUILayout.Width(18), GUILayout.Height(18));

		using (new EditorGUI.DisabledScope(true))
		{
			_defaultPathAsset = (DefaultAsset)EditorGUILayout.ObjectField(
				new GUIContent("Default Path", "Base folder for new scripts"),
				_defaultPathAsset, typeof(DefaultAsset), false);
		}

		using (new EditorGUI.DisabledScope(true))
		{
			GUILayout.Space(6);
			var mini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
			EditorGUILayout.LabelField(_lastChosenPath, mini);
		}
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.EndVertical();
	}

	private void DrawLeftEntryList()
	{
		using (new EditorGUILayout.VerticalScope(GUILayout.Width(380)))
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUILayout.Label("Entries", GUILayout.Width(100));
			if (GUILayout.Button("Paste", EditorStyles.toolbarButton, GUILayout.Width(44))) AddFromClipboard();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24))) AddEntry();
			if (GUILayout.Button("Dup", EditorStyles.toolbarButton, GUILayout.Width(36))) DuplicateEntry();
			if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(24))) RemoveEntry();
			EditorGUILayout.EndHorizontal();

			_leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
			for (int i = 0; i < _entries.Count; i++)
			{
				DrawEntryListItem(i);
			}
			EditorGUILayout.EndScrollView();
		}
	}

	private void DrawEntryListItem(int index)
	{
		var e = _entries[index];
		Rect box = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		bool selected = index == _selectedIndex;

		if (selected) EditorGUI.DrawRect(box, new Color(0.24f, 0.49f, 0.91f, 0.20f));
		else if (box.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(box, new Color(1f, 1f, 1f, 0.06f));

		EditorGUILayout.BeginHorizontal();
		string newTitle = EditorGUILayout.TextField(e.displayName);
		if (newTitle != e.displayName)
		{
			e.displayName = newTitle;
			e.explicitFilename = SyncTitleToFilename(newTitle, e.explicitFilename);
		}
		if (GUILayout.Button("▶", GUILayout.Width(24))) _selectedIndex = index;
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		var icon = EditorGUIUtility.IconContent("Folder Icon").image;
		GUILayout.Label(icon, GUILayout.Width(14), GUILayout.Height(14));

		string badgeText;
		Color badgeCol;
		if (e.useCustomPath)
		{
			badgeText = "[custom]";
			badgeCol = new Color(0.8f, 1f, 0.8f, 0.35f);
		}
		else
		{
			var baseTag = Path.GetFileName(_lastChosenPath);
			badgeText = $"[default: {baseTag}]";
			badgeCol = new Color(1f, 1f, 1f, 0.20f);
		}

		var badgeStyle = new GUIStyle(EditorStyles.miniLabel);
		Rect brect = GUILayoutUtility.GetRect(new GUIContent(badgeText), badgeStyle);
		EditorGUI.DrawRect(brect, badgeCol);
		GUI.Label(brect, badgeText, badgeStyle);

		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();

		{
			string effDir = e.GetEffectivePath(_lastChosenPath);
			string fileName = string.IsNullOrWhiteSpace(e.explicitFilename) ? "(auto)" : e.explicitFilename;
			string effRelPath = NormalizeAssetPath(Path.Combine(effDir, FileUtils.EnsureCsExtension(fileName)));
			bool exists = File.Exists(ToAbsolutePath(effRelPath));

			EditorGUILayout.BeginHorizontal();
			var mini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false, clipping = TextClipping.Clip };
			var content = new GUIContent(effRelPath + (exists ? "  (exists)" : ""), effRelPath);
			EditorGUILayout.LabelField(content, mini, GUILayout.ExpandWidth(true));

			if (GUILayout.Button("Copy", GUILayout.Width(46)))
				EditorGUIUtility.systemCopyBuffer = effRelPath;

			EditorGUILayout.EndHorizontal();
		}

		if (!string.IsNullOrEmpty(e.lastStatus))
		{
			var mini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
			Color c = e.lastStatusType == MessageType.Error ? new Color(1f, 0.5f, 0.5f)
					 : e.lastStatusType == MessageType.Warning ? new Color(1f, 0.94f, 0.6f)
					 : new Color(0.8f, 1f, 0.8f);
			Rect statusRect = GUILayoutUtility.GetRect(new GUIContent(e.lastStatus), mini);
			EditorGUI.DrawRect(statusRect, c * 0.25f);
			EditorGUI.LabelField(statusRect, e.lastStatus, mini);
		}

		EditorGUILayout.EndVertical();
		GUILayout.Space(2);
	}

	private static string SyncTitleToFilename(string title, string prevFilename)
	{
		if (string.IsNullOrWhiteSpace(title)) return prevFilename;
		return FileUtils.SanitizeFilename(title);
	}

	private void AddEntry()
	{
		var e = new FileEntry();
		e.explicitFilename = e.displayName;
		e.RebindTo(_lastChosenPath);
		_entries.Add(e);
		_selectedIndex = _entries.Count - 1;
	}

	private void AddFromClipboard()
	{
		var text = EditorGUIUtility.systemCopyBuffer;
		if (string.IsNullOrEmpty(text)) return;

		string detectedName = DetermineFilename(text, "");
		string displayName = string.IsNullOrEmpty(detectedName) ? "Pasted Script" : detectedName;

		var e = new FileEntry { code = text, displayName = displayName };
		e.explicitFilename = displayName;
		e.RebindTo(_lastChosenPath);
		_entries.Add(e);
		_selectedIndex = _entries.Count - 1;
	}

	private void DuplicateEntry()
	{
		if (_selectedIndex < 0 || _selectedIndex >= _entries.Count) return;
		var src = _entries[_selectedIndex];
		var copy = new FileEntry
		{
			displayName = src.displayName + " Copy",
			explicitFilename = src.explicitFilename + " Copy",
			code = src.code,
			useCustomPath = src.useCustomPath,
			customSavePath = src.customSavePath,
			stickToDefault = src.stickToDefault,
			stickyDefaultPath = src.stickyDefaultPath
		};
		_entries.Insert(_selectedIndex + 1, copy);
		_selectedIndex++;
	}

	private void RemoveEntry()
	{
		if (_selectedIndex < 0 || _selectedIndex >= _entries.Count) return;
		_entries.RemoveAt(_selectedIndex);
		_selectedIndex = Mathf.Clamp(_selectedIndex - 1, 0, _entries.Count - 1);
	}

	private void DrawRightEntryDetail()
	{
		using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
		{
			if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
			{
				GUILayout.FlexibleSpace();
				EditorGUILayout.HelpBox("Select or add an entry on the left.", MessageType.Info);
				GUILayout.FlexibleSpace();
				return;
			}

			var e = _entries[_selectedIndex];

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField("Entry Detail", EditorStyles.boldLabel);

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("FileName:", GUILayout.Width(100));
			string newName = EditorGUILayout.TextField(e.explicitFilename);
			if (newName != e.explicitFilename)
			{
				e.explicitFilename = FileUtils.SanitizeFilename(newName);
				e.displayName = e.explicitFilename;
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(2);
			EditorGUILayout.LabelField("Save Path Policy", EditorStyles.boldLabel);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(new GUIContent("↻ Rebind to Default", "현재 Default Path로 이 엔트리의 sticky default를 재설정")))
			{
				e.RebindTo(_lastChosenPath);
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			e.useCustomPath = EditorGUILayout.ToggleLeft("Use Custom Save Path", e.useCustomPath, GUILayout.Width(170));
			using (new EditorGUI.DisabledScope(!e.useCustomPath))
			{
				e.customSavePath = EditorGUILayout.TextField(e.customSavePath);
				if (GUILayout.Button("Browse", GUILayout.Width(72)))
				{
					ProjectFolderSelector.ShowWindow(
						e.useCustomPath && !string.IsNullOrWhiteSpace(e.customSavePath) ? e.customSavePath :
						(string.IsNullOrWhiteSpace(e.stickyDefaultPath) ? _lastChosenPath : e.stickyDefaultPath),
						(selectedPath) =>
						{
							e.customSavePath = selectedPath;
							e.useCustomPath = true;
							_lastChosenPath = selectedPath;
							_defaultPathAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_lastChosenPath);
							Repaint();
						});
				}
			}
			EditorGUILayout.EndHorizontal();

			using (new EditorGUI.DisabledScope(true))
				EditorGUILayout.TextField("Sticky Default Path", string.IsNullOrWhiteSpace(e.stickyDefaultPath) ? "(unset)" : e.stickyDefaultPath);

			EditorGUILayout.EndVertical();

			EditorGUILayout.LabelField("C# Code:");
			_codeScroll = EditorGUILayout.BeginScrollView(_codeScroll, GUILayout.ExpandHeight(true));
			e.code = EditorGUILayout.TextArea(e.code, GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space(6);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Detect FileName From Code", GUILayout.Height(26)))
			{
				var name = DetermineFilename(e.code, e.explicitFilename);
				if (!string.IsNullOrEmpty(name))
				{
					e.explicitFilename = name;
					e.displayName = name;
					e.lastStatus = $"Detected: {name}.cs";
					e.lastStatusType = MessageType.Info;
				}
				else
				{
					e.lastStatus = "Could not detect filename.";
					e.lastStatusType = MessageType.Warning;
				}
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Generate This File", GUILayout.Height(28), GUILayout.Width(180)))
			{
				GenerateSingle(e);
			}
			EditorGUILayout.EndHorizontal();
		}
	}

	private void DrawBottomBar()
	{
		EditorGUILayout.Space(4);
		EditorGUILayout.BeginHorizontal();
		GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
		if (GUILayout.Button($"Generate All ({_entries.Count})", GUILayout.Height(32)))
		{
			GenerateAll();
		}
		GUI.backgroundColor = Color.white;

		GUILayout.FlexibleSpace();

		using (new EditorGUI.DisabledScope(true))
			GUILayout.Button($"Default Path: {_lastChosenPath}", GUILayout.Height(24));

		EditorGUILayout.EndHorizontal();
	}

	private void DrawSavePlanPanel()
	{
		EditorGUILayout.Space(4);
		_showSavePlan = EditorGUILayout.Foldout(_showSavePlan, "Save Plan Preview", true);
		if (!_showSavePlan) return;

		var rows = new List<(string name, string relPath, bool exists)>();
		IEnumerable<FileEntry> targets = _autoSplitOnSave ? SplitEntriesFromList(_entries) : _entries;

		foreach (var e in targets)
		{
			string name = DetermineFilename(e.code, e.explicitFilename) ?? "(auto)";
			name = FileUtils.EnsureCsExtension(name);
			string dir = e.GetEffectivePath(_lastChosenPath);
			string rel = NormalizeAssetPath(Path.Combine(dir, name));
			bool exists = File.Exists(ToAbsolutePath(rel));
			rows.Add((name, rel, exists));
		}

		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		GUILayout.Label("File Name", GUILayout.Width(320));
		GUILayout.Label("Target Path", GUILayout.ExpandWidth(true));
		GUILayout.Label("Status", GUILayout.Width(80));
		EditorGUILayout.EndHorizontal();

		_savePlanScroll = EditorGUILayout.BeginScrollView(_savePlanScroll, true, true, GUILayout.Height(180));

		var rowStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
		for (int i = 0; i < rows.Count; i++)
		{
			var (nm, path, ex) = rows[i];
			Rect r = EditorGUILayout.BeginHorizontal();
			if ((i & 1) == 1) EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, EditorGUIUtility.isProSkin ? 0.035f : 0.08f));

			GUILayout.Label(nm, rowStyle, GUILayout.Width(320));
			GUILayout.Label(new GUIContent(path, path), rowStyle, GUILayout.MinWidth(600));
			GUILayout.Label(ex ? "exists" : "new", GUILayout.Width(80));

			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.EndScrollView();
	}

	private void DrawStatusMessage()
	{
		if (!_status.HasMessage) return;
		EditorGUILayout.Space(4);
		EditorGUILayout.HelpBox(_status.Message, _status.Type);
	}

	private void GenerateSingle(FileEntry e)
	{
		_askMemory = default;
		List<FileEntry> genTargets = new List<FileEntry>();
		if (_autoSplitOnSave)
		{
			var split = SplitEntriesFromEntry(e);
			genTargets.AddRange(split.Count > 0 ? split : new[] { e });
		}
		else genTargets.Add(e);

		int ok = 0, skip = 0, fail = 0;
		foreach (var t in genTargets)
		{
			var res = GenerateFileInternal(t);
			if (res.ok) ok++;
			else if (res.skipped) skip++;
			else fail++;
		}
		_status.Show($"Single generate done. OK:{ok}, Skipped:{skip}, Failed:{fail}",
			fail > 0 ? MessageType.Warning : MessageType.Info);
		Repaint();
	}

	private void GenerateAll()
	{
		if (_entries.Count == 0)
		{
			_status.Show("No entries to generate.", MessageType.Warning);
			return;
		}

		_askMemory = default;
		var targets = _autoSplitOnSave ? SplitEntriesFromList(_entries) : new List<FileEntry>(_entries);

		int ok = 0, skip = 0, fail = 0;
		foreach (var e in targets)
		{
			var res = GenerateFileInternal(e);
			if (res.ok) ok++;
			else if (res.skipped) skip++;
			else fail++;
		}

		_status.Show($"Batch done. OK: {ok}, Skipped: {skip}, Failed: {fail}",
			fail > 0 ? MessageType.Warning : MessageType.Info);
		Repaint();
	}

	private GenResult GenerateFileInternal(FileEntry entry)
	{
		if (string.IsNullOrWhiteSpace(entry.code))
		{
			entry.lastStatus = "Empty code.";
			entry.lastStatusType = MessageType.Error;
			return new GenResult { ok = false, globalMessage = entry.lastStatus, globalType = MessageType.Error };
		}

		string filename = DetermineFilename(entry.code, entry.explicitFilename);
		if (string.IsNullOrEmpty(filename))
		{
			entry.lastStatus = "Could not determine filename.";
			entry.lastStatusType = MessageType.Error;
			return new GenResult { ok = false, globalMessage = entry.lastStatus, globalType = MessageType.Error };
		}

		filename = FileUtils.EnsureCsExtension(filename);

		string targetRelDir = entry.GetEffectivePath(_lastChosenPath);
		string targetRelPath = NormalizeAssetPath(Path.Combine(targetRelDir, filename));
		string targetAbsPath = ToAbsolutePath(targetRelPath);

		FileUtils.EnsureDirectoryExists(Path.GetDirectoryName(targetAbsPath));

		if (File.Exists(targetAbsPath))
		{
			switch (_batchOverwritePolicy)
			{
				case OverwritePolicy.OverwriteAll: break;
				case OverwritePolicy.SkipAll:
					entry.lastStatus = $"Skipped (exists): {targetRelPath}";
					entry.lastStatusType = MessageType.Warning;
					return new GenResult { ok = false, skipped = true, globalMessage = entry.lastStatus, globalType = MessageType.Warning };
				case OverwritePolicy.RenameIfExists:
					targetAbsPath = FileUtils.GetUniquePath(targetAbsPath);
					targetRelPath = ToRelativeAssetPath(targetAbsPath);
					break;
				case OverwritePolicy.Ask:
				default:
					if (_askMemory.hasDecision && _askMemory.applyToAll)
					{
						if (!_askMemory.overwrite)
						{
							entry.lastStatus = $"Skipped (exists): {targetRelPath}";
							entry.lastStatusType = MessageType.Warning;
							return new GenResult { ok = false, skipped = true, globalMessage = entry.lastStatus, globalType = MessageType.Warning };
						}
					}
					else
					{
						int choice = EditorUtility.DisplayDialogComplex(
							"File Exists", $"'{targetRelPath}' already exists.", "Overwrite", "Skip", "Rename New");
						bool applyAll = EditorUtility.DisplayDialog("Apply to All?", "Apply choice to all?", "Yes", "No");
						_askMemory.hasDecision = true;
						_askMemory.applyToAll = applyAll;
						_askMemory.overwrite = (choice == 0);

						if (choice == 1)
						{
							entry.lastStatus = $"Skipped (exists): {targetRelPath}";
							entry.lastStatusType = MessageType.Warning;
							return new GenResult { ok = false, skipped = true, globalMessage = entry.lastStatus, globalType = MessageType.Warning };
						}
						else if (choice == 2)
						{
							targetAbsPath = FileUtils.GetUniquePath(targetAbsPath);
							targetRelPath = ToRelativeAssetPath(targetAbsPath);
						}
					}
					break;
			}
		}

		try
		{
			File.WriteAllText(targetAbsPath, entry.code);
			AssetDatabase.ImportAsset(targetRelPath);
			entry.lastStatus = $"Created: {targetRelPath}";
			entry.lastStatusType = MessageType.Info;

			var asset = AssetDatabase.LoadAssetAtPath<Object>(targetRelPath);
			if (asset != null) EditorGUIUtility.PingObject(asset);

			return new GenResult { ok = true, globalMessage = entry.lastStatus, globalType = MessageType.Info };
		}
		catch (System.Exception ex)
		{
			entry.lastStatus = $"Error: {ex.Message}";
			entry.lastStatusType = MessageType.Error;
			return new GenResult { ok = false, globalMessage = entry.lastStatus, globalType = MessageType.Error };
		}
	}

	private struct GenResult { public bool ok; public bool skipped; public string globalMessage; public MessageType globalType; }

	private static string NormalizeAssetPath(string relPath) => relPath.Replace('\\', '/');

	private static string ToAbsolutePath(string assetsRelativePath)
	{
		if (!assetsRelativePath.StartsWith("Assets")) return Path.GetFullPath(assetsRelativePath);
		string tail = assetsRelativePath.Substring("Assets".Length).TrimStart('/', '\\');
		return Path.Combine(Application.dataPath, tail);
	}

	private static string ToRelativeAssetPath(string absolutePath)
	{
		string dataPath = Application.dataPath.Replace('\\', '/');
		absolutePath = absolutePath.Replace('\\', '/');
		if (absolutePath.StartsWith(dataPath))
		{
			string tail = absolutePath.Substring(dataPath.Length).TrimStart('/');
			return string.IsNullOrEmpty(tail) ? "Assets" : ("Assets/" + tail);
		}
		return absolutePath;
	}

	private string GetDirectoryFromPath(string path)
	{
		string directory = Path.GetDirectoryName(path);
		return string.IsNullOrEmpty(directory) ? "Assets" : directory.Replace('\\', '/');
	}

	// ---------- Auto Split (Fixed Logic) ----------
	private List<FileEntry> SplitEntriesFromEntry(FileEntry src)
	{
		var (usings, typeBlocks) = TypeDetector.ExtractUsingsAndTypeBlocks(src.code);
		var results = new List<FileEntry>();
		if (typeBlocks.Count == 0) return results;

		foreach (var b in typeBlocks)
		{
			string merged = TypeDetector.MergeUsings(usings, b.codeBlock);
			var e = new FileEntry
			{
				displayName = b.name,
				explicitFilename = b.name,
				code = merged,
				useCustomPath = src.useCustomPath,
				customSavePath = src.customSavePath,
				stickToDefault = src.stickToDefault,
				stickyDefaultPath = src.stickyDefaultPath
			};
			results.Add(e);
		}
		return results;
	}

	private List<FileEntry> SplitEntriesFromList(List<FileEntry> list)
	{
		var results = new List<FileEntry>();
		foreach (var src in list)
		{
			var split = SplitEntriesFromEntry(src);
			if (split.Count > 0) results.AddRange(split);
			else results.Add(src);
		}
		return results;
	}

	private string DetermineFilename(string code, string explicitName)
	{
		if (!string.IsNullOrWhiteSpace(explicitName)) return FileUtils.SanitizeFilename(explicitName);
		var detector = new TypeDetector(code);
		return detector.GetMainTypeName();
	}

	private class StatusMessage
	{
		public string Message { get; private set; } = "";
		public MessageType Type { get; private set; } = MessageType.None;
		public bool HasMessage => !string.IsNullOrEmpty(Message);
		public void Show(string message, MessageType type) { Message = message; Type = type; }
	}

	// ---------- Type Detector (Improved) ----------
	private class TypeDetector
	{
		private readonly string _code;
		private const string TYPE_DEFINITION_PATTERN =
			@"(?:public\s+|private\s+|internal\s+|protected\s+)?\s*(?:partial\s+|sealed\s+|static\s+|abstract\s+|readonly\s+)?\s*(class|interface|struct|enum)\s+(\w+)(?:\s*:\s*([\w\s,.<>]+))?";

		private static readonly Regex RxUsing = new Regex(@"^\s*using\s+[^;]+;\s*$", RegexOptions.Multiline);

		public TypeDetector(string code) { _code = code ?? ""; }

		public string GetMainTypeName()
		{
			var types = ParseTypes();
			if (types.Count == 0) return null;
			var mainType = types
				.OrderBy(t => t.IsUnityClass ? 0 : 1)
				.ThenBy(t => t.TypeKeyword == "class" ? 0 : t.TypeKeyword == "interface" ? 1 : t.TypeKeyword == "struct" ? 2 : 3)
				.ThenByDescending(t => t.LineCount)
				.FirstOrDefault();
			return mainType?.Name;
		}

		public static (List<string> usings, List<(string name, string codeBlock)> blocks)
			ExtractUsingsAndTypeBlocks(string code)
		{
			code ??= "";
			var usings = RxUsing.Matches(code).Cast<Match>().Select(m => m.Value.Trim()).Distinct().ToList();
			var blocks = new List<(string, string)>();
			var matches = Regex.Matches(code, TYPE_DEFINITION_PATTERN);

			foreach (Match m in matches)
			{
				var name = m.Groups[2].Value;
				var bounds = FindTypeBodyBounds(code, m.Index);
				if (bounds.HasValue)
				{
					// FIXED: 클래스 정의 윗부분(Attribute, 주석)을 찾아 시작점(Start)을 앞당김
					int extendedStart = GetExtendedStartIndex(code, m.Index);
					int end = bounds.Value.end + 1;
					string block = SafeSub(code, extendedStart, end - extendedStart);
					blocks.Add((name, block));
				}
			}
			return (usings, blocks);
		}

		// 클래스 정의부 위로 올라가며 속성([])이나 주석(///, //)을 포함시킴
		private static int GetExtendedStartIndex(string code, int typeDefIndex)
		{
			// typeDefIndex는 "public class..."의 시작점
			// 이 앞쪽 라인을 검사
			if (typeDefIndex <= 0) return 0;

			// 코드를 라인 단위로 나누지 않고, 인덱스로 뒤로 검색
			// 안전하게: 이전 '}' 나 ';'가 나오면 그 다음이 시작점.

			int curr = typeDefIndex - 1;
			while (curr >= 0)
			{
				char c = code[curr];
				// 이전 블록의 끝(}) 혹은 문장의 끝(;)을 만나면 멈춤
				if (c == '}' || c == ';')
				{
					return curr + 1;
				}
				curr--;
			}
			return 0; // 파일 시작
		}

		public static string MergeUsings(List<string> headerUsings, string codeBlock)
		{
			codeBlock ??= "";
			var existing = RxUsing.Matches(codeBlock).Cast<Match>().Select(m => m.Value.Trim()).ToHashSet();
			var merged = new List<string>();
			foreach (var u in headerUsings) if (!existing.Contains(u)) merged.Add(u);
			merged.AddRange(RxUsing.Matches(codeBlock).Cast<Match>().Select(m => m.Value.Trim()));
			string body = RxUsing.Replace(codeBlock, "").TrimStart();
			return string.Join("\n", merged.Distinct()) + "\n\n" + body;
		}

		private static string SafeSub(string s, int start, int len)
		{
			if (start < 0 || start >= s.Length) return "";
			if (start + len > s.Length) len = s.Length - start;
			return s.Substring(start, len);
		}

		private List<TypeInfo> ParseTypes()
		{
			var matches = Regex.Matches(_code, TYPE_DEFINITION_PATTERN);
			return matches.Cast<Match>().Select(ParseTypeFromMatch).ToList();
		}

		private TypeInfo ParseTypeFromMatch(Match match)
		{
			var info = new TypeInfo
			{
				TypeKeyword = match.Groups[1].Value,
				Name = match.Groups[2].Value
			};
			if (match.Groups[3].Success && info.TypeKeyword == "class")
				info.IsUnityClass = IsUnityClass(match.Groups[3].Value);
			info.LineCount = CountTypeLines(match.Index);
			return info;
		}

		private bool IsUnityClass(string inheritance) => UNITY_BASE_CLASSES.Any(baseClass => inheritance.Contains(baseClass));

		private int CountTypeLines(int typeStart)
		{
			var bounds = FindTypeBodyBounds(_code, typeStart);
			if (!bounds.HasValue) return 0;
			string body = _code.Substring(bounds.Value.start, bounds.Value.end - bounds.Value.start);
			return body.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line));
		}

		private static (int start, int end)? FindTypeBodyBounds(string source, int typeStartIndex)
		{
			int braceDepth = 0;
			int bodyStart = -1;
			int firstBraceIndex = source.IndexOf('{', typeStartIndex);
			if (firstBraceIndex == -1) return null;

			for (int i = firstBraceIndex; i < source.Length; i++)
			{
				if (source[i] == '{')
				{
					if (braceDepth == 0) bodyStart = i + 1;
					braceDepth++;
				}
				else if (source[i] == '}')
				{
					braceDepth--;
					if (braceDepth == 0 && bodyStart != -1) return (bodyStart, i);
				}
			}
			return null;
		}

		private class TypeInfo { public string Name; public string TypeKeyword; public bool IsUnityClass; public int LineCount; }
	}

	private static class FileUtils
	{
		private static readonly char[] INVALID_CHARS = Path.GetInvalidFileNameChars();
		public static string SanitizeFilename(string filename)
		{
			string sanitized = string.Join("", (filename ?? "").Split(INVALID_CHARS));
			return sanitized.EndsWith(".cs") ? sanitized.Substring(0, sanitized.Length - 3) : sanitized;
		}
		public static string EnsureCsExtension(string filename) => filename.EndsWith(".cs") ? filename : filename + ".cs";
		public static void EnsureDirectoryExists(string directory) { if (!Directory.Exists(directory)) Directory.CreateDirectory(directory); }
		public static string GetUniquePath(string absPath)
		{
			string dir = Path.GetDirectoryName(absPath);
			string name = Path.GetFileNameWithoutExtension(absPath);
			string ext = Path.GetExtension(absPath);
			int i = 1;
			string candidate;
			do { candidate = Path.Combine(dir, $"{name} ({i}){ext}"); i++; } while (File.Exists(candidate));
			return candidate;
		}
	}
}

// ===================== Project Folder Selector =====================
public class ProjectFolderSelector : EditorWindow
{
	private System.Action<string> _onFolderSelected;
	private string _selectedPath;
	private Vector2 _scrollPosition;
	private Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();
	private List<FolderNode> _rootFolders;

	private static readonly Vector2 WINDOW_SIZE = new Vector2(400, 500);

	public static void ShowWindow(string currentPath, System.Action<string> onFolderSelected)
	{
		var window = CreateInstance<ProjectFolderSelector>();
		window.titleContent = new GUIContent("Select Folder");
		window._onFolderSelected = onFolderSelected;
		window._selectedPath = string.IsNullOrEmpty(currentPath) ? "Assets" : currentPath;
		window.position = new Rect((Screen.currentResolution.width - WINDOW_SIZE.x) / 2f, (Screen.currentResolution.height - WINDOW_SIZE.y) / 2f, WINDOW_SIZE.x, WINDOW_SIZE.y);
		window.BuildFolderTree();
		window.ShowModal();
	}
	private void BuildFolderTree()
	{
		_rootFolders = new List<FolderNode>();
		var assetsNode = new FolderNode("Assets", "Assets");
		BuildFolderNode(assetsNode);
		_rootFolders.Add(assetsNode);
		SetInitialFoldoutStates();
	}
	private void BuildFolderNode(FolderNode node)
	{
		try
		{
			var directories = Directory.GetDirectories(node.FullPath).Where(d => !Path.GetFileName(d).StartsWith(".")).OrderBy(d => Path.GetFileName(d));
			foreach (var dir in directories)
			{
				string folderName = Path.GetFileName(dir);
				string relativePath = dir.Replace(Application.dataPath, "Assets").Replace('\\', '/');
				var childNode = new FolderNode(folderName, relativePath);
				BuildFolderNode(childNode);
				node.Children.Add(childNode);
			}
		}
		catch { }
	}
	private void SetInitialFoldoutStates()
	{
		string[] parts = _selectedPath.Split('/');
		string current = "";
		foreach (var p in parts) { if (string.IsNullOrEmpty(p)) continue; current = string.IsNullOrEmpty(current) ? p : current + "/" + p; _foldoutStates[current] = true; }
	}
	private void OnGUI()
	{
		GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
		EditorGUILayout.LabelField("Select a folder to save:", titleStyle);
		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		EditorGUILayout.LabelField("Selected:", EditorStyles.miniLabel, GUILayout.Width(60));
		EditorGUILayout.LabelField(_selectedPath, EditorStyles.miniLabel);
		EditorGUILayout.EndHorizontal();
		Rect treeRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		EditorGUI.DrawRect(treeRect, EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 1f) : new Color(0.76f, 0.76f, 0.76f, 1f));
		_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
		foreach (var root in _rootFolders) DrawFolderNode(root, 0);
		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndVertical();
		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(70))) { _onFolderSelected?.Invoke(_selectedPath); Close(); }
		if (GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(70))) { Close(); }
		EditorGUILayout.EndHorizontal();
	}
	private void DrawFolderNode(FolderNode node, int depth)
	{
		bool isSelected = node.RelativePath == _selectedPath;
		bool hasFoldout = node.Children.Count > 0;
		bool isFoldedOut = _foldoutStates.ContainsKey(node.RelativePath) ? _foldoutStates[node.RelativePath] : false;
		Rect itemRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(18));
		if (isSelected) EditorGUI.DrawRect(itemRect, new Color(0.24f, 0.49f, 0.91f, 0.5f));
		else if (itemRect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(itemRect, new Color(1f, 1f, 1f, 0.08f));
		GUILayout.Space(depth * 14);
		if (hasFoldout) { bool newState = EditorGUILayout.Toggle(isFoldedOut, EditorStyles.foldout, GUILayout.Width(14)); if (newState != isFoldedOut) _foldoutStates[node.RelativePath] = newState; }
		else GUILayout.Space(14);
		Texture2D folderIcon = GetFolderIcon(node, isFoldedOut);
		if (folderIcon != null) GUILayout.Label(folderIcon, GUILayout.Width(16), GUILayout.Height(16)); else GUILayout.Space(16);
		GUILayout.Space(2);
		GUIStyle labelStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black } };
		if (GUILayout.Button(node.Name, labelStyle)) _selectedPath = node.RelativePath;
		EditorGUILayout.EndHorizontal();
		if (hasFoldout && isFoldedOut) foreach (var child in node.Children) DrawFolderNode(child, depth + 1);
	}
	private Texture2D GetFolderIcon(FolderNode node, bool isExpanded)
	{
		string iconName = isExpanded ? "FolderOpened Icon" : "Folder Icon";
		var icon = EditorGUIUtility.IconContent(iconName)?.image as Texture2D;
		if (icon != null) return icon;
		return EditorGUIUtility.IconContent("Folder Icon")?.image as Texture2D;
	}
	private class FolderNode { public string Name; public string RelativePath; public string FullPath; public List<FolderNode> Children; public FolderNode(string name, string relativePath) { Name = name; RelativePath = relativePath; FullPath = relativePath == "Assets" ? Application.dataPath : Application.dataPath + relativePath.Substring(6); Children = new List<FolderNode>(); } }
}