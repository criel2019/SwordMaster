using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Text;
using System.IO;

// 메뉴 타입 열거형
    public enum MenuType
    {
        Unity,
        Plugin,
        Asset,
        Developer
    }