# Phase 3a — WPF Shell + Dashboard Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 Application 레이어(CanEventHub, RawFrameStore, BusLifecycleService, IAlarmEngine, ManualBusStatusPublisher)를 소비하는 WPF Shell을 최초 구성한다. Virtual 어댑터로 Connect 시 상단 LED가 녹색으로, 하단 상태바 Rx/s 카운터가 EEC1 100 ms 주기에 따라 흐르는 상태까지가 완료 기준.

**Architecture:** `src/Wpf/` 단일 WPF WinExe 프로젝트 (net8.0-windows, UseWPF, CommunityToolkit.Mvvm + Host Builder DI). 하이브리드 레이아웃 — 상단 글로벌 세션바 · 좌측 56 px Rail · 중앙 Content · 하단 상태바. Dashboard 탭만 WrapPanel placeholder 3개를 띄우고 나머지 6개 탭은 공통 `PlaceholderView` 로 스텁. ViewModel 3개 — `ShellViewModel`(네비 호스트) / `SessionViewModel`(어댑터·DBC·Connect) / `StatusBarViewModel`(Rx · Tx · Drop · Alarm). Application 레이어는 Wpf 참조 0.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm (ObservableObject · RelayCommand · ObservableProperty), Microsoft.Extensions.Hosting, Microsoft.Extensions.DependencyInjection, System.Reactive, xUnit + FluentAssertions + Microsoft.Reactive.Testing (VM 단위 테스트), Segoe Fluent Icons (Windows 11 내장).

**Reference Spec:** `docs/superpowers/specs/2026-04-22-phase3a-shell-design.md`

---

## File Structure

**신규 소스 (`src/Wpf/`)**
- `CanMonitor.Wpf.csproj`
- `App.xaml`, `App.xaml.cs` — Host builder + ShellWindow 표시
- `Shell/ShellWindow.xaml`, `ShellWindow.xaml.cs`
- `Shell/ShellViewModel.cs`
- `Shell/SessionViewModel.cs`
- `Shell/StatusBarViewModel.cs`
- `Navigation/INavTarget.cs`
- `Navigation/DashboardNavTarget.cs`
- `Navigation/PlaceholderNavTarget.cs`
- `Navigation/PlaceholderViewModel.cs`
- `Navigation/PlaceholderView.xaml`, `PlaceholderView.xaml.cs`
- `Dashboard/IDashboardWidget.cs`
- `Dashboard/DashboardView.xaml`, `DashboardView.xaml.cs`
- `Dashboard/DashboardViewModel.cs`
- `Dashboard/Widgets/PlaceholderWidget.cs`
- `Dashboard/Widgets/PlaceholderWidgetViewModel.cs`
- `Dashboard/Widgets/PlaceholderWidgetView.xaml`, `PlaceholderWidgetView.xaml.cs`
- `Infrastructure/CanBusFactory.cs`
- `Infrastructure/ICanBusFactory.cs`
- `Infrastructure/AdapterKind.cs`
- `Infrastructure/AdapterOption.cs`
- `Infrastructure/DbcFileOption.cs`
- `Infrastructure/ConnectionState.cs`
- `Themes/Colors.xaml`
- `Themes/Light.xaml`
- `Themes/Styles/Controls.xaml`
- `Themes/Styles/Typography.xaml`
- `Controls/LedIndicator.xaml`, `LedIndicator.xaml.cs`

**신규 테스트 (`tests/Wpf.Tests/`)**
- `CanMonitor.Wpf.Tests.csproj`
- `Shell/SessionViewModelTests.cs`
- `Shell/StatusBarViewModelTests.cs`
- `Shell/ShellViewModelTests.cs`
- `Dashboard/DashboardViewModelTests.cs`
- `Infrastructure/CanBusFactoryTests.cs`

**수정 대상**
- `CanMonitor.sln` — src/Wpf, tests/Wpf.Tests 프로젝트 추가

---

## Task 1: WPF 프로젝트 + 테스트 프로젝트 스캐폴드

**목표:** 빈 WPF WinExe + xUnit 테스트 프로젝트가 빌드되고, 솔루션에 등록되어 `dotnet build CanMonitor.sln` 이 통과한다. App.xaml.cs 는 아직 DI 없이 빈 Window 만 띄운다.

**Files:**
- Create: `src/Wpf/CanMonitor.Wpf.csproj`
- Create: `src/Wpf/App.xaml`
- Create: `src/Wpf/App.xaml.cs`
- Create: `src/Wpf/Shell/ShellWindow.xaml`
- Create: `src/Wpf/Shell/ShellWindow.xaml.cs`
- Create: `tests/Wpf.Tests/CanMonitor.Wpf.Tests.csproj`
- Create: `tests/Wpf.Tests/SmokeTests.cs`
- Modify: `CanMonitor.sln` (프로젝트 2개 추가)

- [ ] **Step 1: `src/Wpf/CanMonitor.Wpf.csproj` 작성**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <RootNamespace>CanMonitor.Wpf</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Core\CanMonitor.Core.csproj" />
    <ProjectReference Include="..\Application\CanMonitor.Application.csproj" />
    <ProjectReference Include="..\Dbc\CanMonitor.Dbc.csproj" />
    <ProjectReference Include="..\Infrastructure.Can\CanMonitor.Infrastructure.Can.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.1" />
    <PackageReference Include="System.Reactive" Version="6.0.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: `src/Wpf/App.xaml` 작성**

```xml
<Application x:Class="CanMonitor.Wpf.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="Shell/ShellWindow.xaml">
    <Application.Resources />
</Application>
```

- [ ] **Step 3: `src/Wpf/App.xaml.cs` 작성**

```csharp
using System.Windows;

namespace CanMonitor.Wpf;

public partial class App : Application
{
}
```

- [ ] **Step 4: `src/Wpf/Shell/ShellWindow.xaml` 작성 (최소)**

```xml
<Window x:Class="CanMonitor.Wpf.Shell.ShellWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="CAN Monitor" Height="720" Width="1280">
    <Grid>
        <TextBlock Text="CAN Monitor - Phase 3a (scaffold)"
                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
    </Grid>
</Window>
```

- [ ] **Step 5: `src/Wpf/Shell/ShellWindow.xaml.cs` 작성**

```csharp
using System.Windows;

namespace CanMonitor.Wpf.Shell;

public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 6: `tests/Wpf.Tests/CanMonitor.Wpf.Tests.csproj` 작성**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Wpf\CanMonitor.Wpf.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <PackageReference Include="Microsoft.Reactive.Testing" Version="6.0.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 7: `tests/Wpf.Tests/SmokeTests.cs` 작성 (프로젝트 로딩 확인용)**

```csharp
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests;

public class SmokeTests
{
    [Fact]
    public void Assembly_is_loaded()
    {
        typeof(App).Assembly.FullName.Should().Contain("CanMonitor.Wpf");
    }
}
```

- [ ] **Step 8: 솔루션에 두 프로젝트 등록**

Run: `dotnet sln CanMonitor.sln add src\Wpf\CanMonitor.Wpf.csproj tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj`
Expected: 두 항목이 "Project added." 로그

- [ ] **Step 9: 빌드 확인**

Run: `dotnet build CanMonitor.sln`
Expected: 경고만 허용, 에러 0, `CanMonitor.Wpf -> ...\net8.0-windows\CanMonitor.Wpf.dll` 출력

- [ ] **Step 10: 스모크 테스트 실행**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 11: Commit**

```bash
git add src/Wpf tests/Wpf.Tests CanMonitor.sln
git commit -m "feat(wpf): scaffold CanMonitor.Wpf project + Wpf.Tests"
```

---

## Task 2: Infrastructure 타입 — AdapterKind / AdapterOption / DbcFileOption / ConnectionState

**목표:** 상단 바 드롭다운과 VM 간 주고받을 값 객체 + 상태 enum을 별도 파일로 분리. 아직 로직 없음, pure data.

**Files:**
- Create: `src/Wpf/Infrastructure/AdapterKind.cs`
- Create: `src/Wpf/Infrastructure/AdapterOption.cs`
- Create: `src/Wpf/Infrastructure/DbcFileOption.cs`
- Create: `src/Wpf/Infrastructure/ConnectionState.cs`

- [ ] **Step 1: `src/Wpf/Infrastructure/AdapterKind.cs`**

```csharp
namespace CanMonitor.Wpf.Infrastructure;

public enum AdapterKind
{
    Virtual
}
```

- [ ] **Step 2: `src/Wpf/Infrastructure/AdapterOption.cs`**

```csharp
namespace CanMonitor.Wpf.Infrastructure;

public sealed record AdapterOption(AdapterKind Kind, string DisplayName);
```

- [ ] **Step 3: `src/Wpf/Infrastructure/DbcFileOption.cs`**

```csharp
namespace CanMonitor.Wpf.Infrastructure;

public enum DbcSource
{
    Confirmed,
    Experimental,
    External
}

public sealed record DbcFileOption(string Path, string DisplayName, DbcSource Source);
```

- [ ] **Step 4: `src/Wpf/Infrastructure/ConnectionState.cs`**

```csharp
namespace CanMonitor.Wpf.Infrastructure;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}
```

- [ ] **Step 5: Build 확인**

Run: `dotnet build src\Wpf\CanMonitor.Wpf.csproj`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add src/Wpf/Infrastructure
git commit -m "feat(wpf): add AdapterKind, AdapterOption, DbcFileOption, ConnectionState"
```

---

## Task 3: `ICanBusFactory` + `CanBusFactory` + 테스트

**목표:** `AdapterKind` → `ICanBus` 팩토리. Phase 3a는 Virtual 만 반환. 테스트 먼저(TDD).

**Files:**
- Create: `src/Wpf/Infrastructure/ICanBusFactory.cs`
- Create: `src/Wpf/Infrastructure/CanBusFactory.cs`
- Create: `tests/Wpf.Tests/Infrastructure/CanBusFactoryTests.cs`

- [ ] **Step 1: 실패 테스트 작성 — `tests/Wpf.Tests/Infrastructure/CanBusFactoryTests.cs`**

```csharp
using CanMonitor.Infrastructure.Can.Virtual;
using CanMonitor.Wpf.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Infrastructure;

public class CanBusFactoryTests
{
    [Fact]
    public void Known_contains_virtual_adapter()
    {
        var factory = new CanBusFactory();
        factory.Known.Should().ContainSingle()
            .Which.Kind.Should().Be(AdapterKind.Virtual);
    }

    [Fact]
    public void Create_virtual_returns_VirtualCanBus()
    {
        var factory = new CanBusFactory();
        var bus = factory.Create(AdapterKind.Virtual);
        bus.Should().BeOfType<VirtualCanBus>();
    }

    [Fact]
    public void Create_unsupported_throws()
    {
        var factory = new CanBusFactory();
        var act = () => factory.Create((AdapterKind)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
```

- [ ] **Step 2: 실행 — 컴파일 실패 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~CanBusFactoryTests`
Expected: 빌드 실패 — `CanBusFactory` 가 정의되지 않음

- [ ] **Step 3: `src/Wpf/Infrastructure/ICanBusFactory.cs` 작성**

```csharp
using CanMonitor.Core.Abstractions;

namespace CanMonitor.Wpf.Infrastructure;

public interface ICanBusFactory
{
    IReadOnlyList<AdapterOption> Known { get; }
    ICanBus Create(AdapterKind kind);
}
```

- [ ] **Step 4: `src/Wpf/Infrastructure/CanBusFactory.cs` 작성**

```csharp
using CanMonitor.Core.Abstractions;
using CanMonitor.Infrastructure.Can.Virtual;

namespace CanMonitor.Wpf.Infrastructure;

public sealed class CanBusFactory : ICanBusFactory
{
    public IReadOnlyList<AdapterOption> Known { get; } = new[]
    {
        new AdapterOption(AdapterKind.Virtual, "Virtual")
    };

    public ICanBus Create(AdapterKind kind) => kind switch
    {
        AdapterKind.Virtual => new VirtualCanBus(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported adapter")
    };
}
```

- [ ] **Step 5: 테스트 실행 — 통과 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~CanBusFactoryTests`
Expected: `Passed: 3, Failed: 0`

- [ ] **Step 6: Commit**

```bash
git add src/Wpf/Infrastructure tests/Wpf.Tests/Infrastructure
git commit -m "feat(wpf): add ICanBusFactory with Virtual adapter support"
```

---

## Task 4: 테마 리소스 (Colors + Light + Styles)

**목표:** 앱 전역 색상 토큰과 기본 스타일을 `ResourceDictionary` 로 분리. Light 팔레트만 매핑, Dark 는 Phase 3b 이후.

**Files:**
- Create: `src/Wpf/Themes/Colors.xaml`
- Create: `src/Wpf/Themes/Light.xaml`
- Create: `src/Wpf/Themes/Styles/Controls.xaml`
- Create: `src/Wpf/Themes/Styles/Typography.xaml`
- Modify: `src/Wpf/App.xaml` — MergedDictionaries 등록

- [ ] **Step 1: `src/Wpf/Themes/Colors.xaml` (의미 토큰 이름만 선언)**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Surface -->
    <Color x:Key="BackgroundColor">#F4F6FA</Color>
    <Color x:Key="SurfaceColor">#FFFFFF</Color>
    <Color x:Key="BorderColor">#D0D6E0</Color>
    <!-- Text -->
    <Color x:Key="TextPrimaryColor">#1B2230</Color>
    <Color x:Key="TextSecondaryColor">#5D6778</Color>
    <Color x:Key="TextMutedColor">#8A94A5</Color>
    <!-- Accent -->
    <Color x:Key="AccentColor">#1F6FEB</Color>
    <Color x:Key="AccentContrastColor">#FFFFFF</Color>
    <!-- Status -->
    <Color x:Key="LedConnectedColor">#2A8F3A</Color>
    <Color x:Key="LedDisconnectedColor">#8A94A5</Color>
    <Color x:Key="LedErrorColor">#A4312E</Color>
    <Color x:Key="WarningColor">#B35A00</Color>
    <!-- Rail -->
    <Color x:Key="RailBackgroundColor">#1E2530</Color>
    <Color x:Key="RailForegroundColor">#C8D3E6</Color>
    <Color x:Key="RailSelectedBackgroundColor">#2A3340</Color>
    <Color x:Key="RailSelectedAccentColor">#4A9EFF</Color>
</ResourceDictionary>
```

- [ ] **Step 2: `src/Wpf/Themes/Light.xaml` (Brush 매핑)**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Colors.xaml" />
    </ResourceDictionary.MergedDictionaries>

    <SolidColorBrush x:Key="BackgroundBrush"       Color="{StaticResource BackgroundColor}"/>
    <SolidColorBrush x:Key="SurfaceBrush"          Color="{StaticResource SurfaceColor}"/>
    <SolidColorBrush x:Key="BorderBrush"           Color="{StaticResource BorderColor}"/>
    <SolidColorBrush x:Key="TextPrimaryBrush"      Color="{StaticResource TextPrimaryColor}"/>
    <SolidColorBrush x:Key="TextSecondaryBrush"    Color="{StaticResource TextSecondaryColor}"/>
    <SolidColorBrush x:Key="TextMutedBrush"        Color="{StaticResource TextMutedColor}"/>
    <SolidColorBrush x:Key="AccentBrush"           Color="{StaticResource AccentColor}"/>
    <SolidColorBrush x:Key="AccentContrastBrush"   Color="{StaticResource AccentContrastColor}"/>
    <SolidColorBrush x:Key="LedConnectedBrush"     Color="{StaticResource LedConnectedColor}"/>
    <SolidColorBrush x:Key="LedDisconnectedBrush"  Color="{StaticResource LedDisconnectedColor}"/>
    <SolidColorBrush x:Key="LedErrorBrush"         Color="{StaticResource LedErrorColor}"/>
    <SolidColorBrush x:Key="WarningBrush"          Color="{StaticResource WarningColor}"/>
    <SolidColorBrush x:Key="RailBackgroundBrush"           Color="{StaticResource RailBackgroundColor}"/>
    <SolidColorBrush x:Key="RailForegroundBrush"           Color="{StaticResource RailForegroundColor}"/>
    <SolidColorBrush x:Key="RailSelectedBackgroundBrush"   Color="{StaticResource RailSelectedBackgroundColor}"/>
    <SolidColorBrush x:Key="RailSelectedAccentBrush"       Color="{StaticResource RailSelectedAccentColor}"/>
</ResourceDictionary>
```

- [ ] **Step 3: `src/Wpf/Themes/Styles/Typography.xaml`**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <FontFamily x:Key="UiFontFamily">Segoe UI</FontFamily>
    <FontFamily x:Key="IconFontFamily">Segoe Fluent Icons</FontFamily>
    <FontFamily x:Key="MonoFontFamily">Consolas</FontFamily>

    <Style x:Key="BodyTextStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource UiFontFamily}"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
    </Style>

    <Style x:Key="MonoTextStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource MonoFontFamily}"/>
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
    </Style>

    <Style x:Key="IconGlyphStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource IconFontFamily}"/>
        <Setter Property="FontSize" Value="18"/>
        <Setter Property="Foreground" Value="{DynamicResource RailForegroundBrush}"/>
    </Style>
</ResourceDictionary>
```

- [ ] **Step 4: `src/Wpf/Themes/Styles/Controls.xaml`**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Style TargetType="Window">
        <Setter Property="Background" Value="{DynamicResource BackgroundBrush}"/>
        <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
        <Setter Property="FontFamily" Value="Segoe UI"/>
        <Setter Property="FontSize" Value="13"/>
    </Style>

    <Style TargetType="Button">
        <Setter Property="Background" Value="{DynamicResource SurfaceBrush}"/>
        <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
        <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="10,4"/>
        <Setter Property="MinHeight" Value="26"/>
    </Style>

    <Style TargetType="ComboBox">
        <Setter Property="MinHeight" Value="26"/>
        <Setter Property="Padding" Value="6,2"/>
    </Style>
</ResourceDictionary>
```

- [ ] **Step 5: `src/Wpf/App.xaml` 수정 — MergedDictionaries 등록 + StartupUri 제거 (Task 14 에서 코드로 띄움)**

```xml
<Application x:Class="CanMonitor.Wpf.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Themes/Light.xaml" />
                <ResourceDictionary Source="Themes/Styles/Typography.xaml" />
                <ResourceDictionary Source="Themes/Styles/Controls.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

(`StartupUri` 는 Task 14 에서 코드로 교체하므로 여기서 지운다.)

- [ ] **Step 6: `App.xaml.cs` 를 StartupUri 제거에 맞춰 수정**

```csharp
using System.Windows;

namespace CanMonitor.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new Shell.ShellWindow().Show();
    }
}
```

- [ ] **Step 7: 빌드 확인**

Run: `dotnet build src\Wpf\CanMonitor.Wpf.csproj`
Expected: 0 errors. `XAML` 리소스 누락 경고 없음.

- [ ] **Step 8: Commit**

```bash
git add src/Wpf/Themes src/Wpf/App.xaml src/Wpf/App.xaml.cs
git commit -m "feat(wpf): add Light theme tokens and control styles"
```

---

## Task 5: `LedIndicator` UserControl

**목표:** 연결 상태를 색상으로 표시하는 16 px 원형 LED. `State` DependencyProperty (`ConnectionState`) 를 바인딩하면 브러시가 바뀐다.

**Files:**
- Create: `src/Wpf/Controls/LedIndicator.xaml`
- Create: `src/Wpf/Controls/LedIndicator.xaml.cs`

- [ ] **Step 1: `src/Wpf/Controls/LedIndicator.xaml`**

```xml
<UserControl x:Class="CanMonitor.Wpf.Controls.LedIndicator"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Name="Root">
    <Ellipse Width="12" Height="12"
             Stroke="{DynamicResource BorderBrush}" StrokeThickness="1"
             Fill="{Binding ElementName=Root, Path=Brush}"/>
</UserControl>
```

- [ ] **Step 2: `src/Wpf/Controls/LedIndicator.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanMonitor.Wpf.Infrastructure;

namespace CanMonitor.Wpf.Controls;

public partial class LedIndicator : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(ConnectionState), typeof(LedIndicator),
        new PropertyMetadata(ConnectionState.Disconnected, OnStateChanged));

    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush), typeof(Brush), typeof(LedIndicator),
        new PropertyMetadata(null));

    public LedIndicator()
    {
        InitializeComponent();
        UpdateBrush();
    }

    public ConnectionState State
    {
        get => (ConnectionState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        private set => SetValue(BrushProperty, value);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LedIndicator)d).UpdateBrush();

    private void UpdateBrush()
    {
        var resourceKey = State switch
        {
            ConnectionState.Connected => "LedConnectedBrush",
            ConnectionState.Error => "LedErrorBrush",
            ConnectionState.Connecting => "WarningBrush",
            _ => "LedDisconnectedBrush"
        };
        Brush = TryFindResource(resourceKey) as Brush;
    }
}
```

- [ ] **Step 3: 빌드 확인**

Run: `dotnet build src\Wpf\CanMonitor.Wpf.csproj`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/Wpf/Controls
git commit -m "feat(wpf): add LedIndicator user control"
```

---

## Task 6: Navigation 인프라 — `INavTarget`, `PlaceholderViewModel`, `PlaceholderView`

**목표:** 7개 탭을 표현할 추상 + 미구현 탭 공용 플레이스홀더 Tab 1개. DashboardNavTarget은 Task 10, PlaceholderNavTarget 은 Task 7 에서.

**Files:**
- Create: `src/Wpf/Navigation/INavTarget.cs`
- Create: `src/Wpf/Navigation/PlaceholderViewModel.cs`
- Create: `src/Wpf/Navigation/PlaceholderView.xaml`
- Create: `src/Wpf/Navigation/PlaceholderView.xaml.cs`

- [ ] **Step 1: `src/Wpf/Navigation/INavTarget.cs`**

```csharp
namespace CanMonitor.Wpf.Navigation;

public interface INavTarget
{
    string Key { get; }
    string Title { get; }
    string IconGlyph { get; }
    int Order { get; }
    object CreateViewModel(IServiceProvider sp);
}
```

- [ ] **Step 2: `src/Wpf/Navigation/PlaceholderViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Navigation;

public sealed partial class PlaceholderViewModel : ObservableObject
{
    public PlaceholderViewModel(string title)
    {
        Title = title;
    }

    public string Title { get; }
    public string Message => $"{Title} — 구현 예정 (Phase 3b 이후).";
}
```

- [ ] **Step 3: `src/Wpf/Navigation/PlaceholderView.xaml`**

```xml
<UserControl x:Class="CanMonitor.Wpf.Navigation.PlaceholderView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Background="{DynamicResource BackgroundBrush}">
        <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
            <TextBlock Text="{Binding Title}"
                       FontSize="20" FontWeight="SemiBold"
                       Foreground="{DynamicResource TextPrimaryBrush}"
                       HorizontalAlignment="Center"/>
            <TextBlock Text="{Binding Message}"
                       Foreground="{DynamicResource TextSecondaryBrush}"
                       Margin="0,8,0,0"
                       HorizontalAlignment="Center"/>
        </StackPanel>
    </Grid>
</UserControl>
```

- [ ] **Step 4: `src/Wpf/Navigation/PlaceholderView.xaml.cs`**

```csharp
using System.Windows.Controls;

namespace CanMonitor.Wpf.Navigation;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView() => InitializeComponent();
}
```

- [ ] **Step 5: 빌드 확인**

Run: `dotnet build src\Wpf\CanMonitor.Wpf.csproj`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add src/Wpf/Navigation
git commit -m "feat(wpf): add INavTarget contract and Placeholder view/viewmodel"
```

---

## Task 7: `PlaceholderNavTarget`

**목표:** 6개 미구현 탭을 위한 단순 `INavTarget` 구현체. `CreateViewModel` 은 새 `PlaceholderViewModel(title)` 반환.

**Files:**
- Create: `src/Wpf/Navigation/PlaceholderNavTarget.cs`
- Create: `tests/Wpf.Tests/Navigation/PlaceholderNavTargetTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using CanMonitor.Wpf.Navigation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanMonitor.Wpf.Tests.Navigation;

public class PlaceholderNavTargetTests
{
    [Fact]
    public void CreateViewModel_returns_Placeholder_with_title()
    {
        var target = new PlaceholderNavTarget("Raw", "BulletedList", "Raw Log", 20);
        var sp = new ServiceCollection().BuildServiceProvider();
        var vm = target.CreateViewModel(sp);
        vm.Should().BeOfType<PlaceholderViewModel>()
          .Which.Title.Should().Be("Raw Log");
    }

    [Fact]
    public void Properties_reflect_constructor_arguments()
    {
        var target = new PlaceholderNavTarget("Transmit", "Send", "Transmit", 30);
        target.Key.Should().Be("Transmit");
        target.Title.Should().Be("Transmit");
        target.IconGlyph.Should().Be("Send");
        target.Order.Should().Be(30);
    }
}
```

- [ ] **Step 2: 테스트 실행 — 컴파일 실패 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~PlaceholderNavTargetTests`
Expected: 빌드 실패 (`PlaceholderNavTarget` 미정의)

- [ ] **Step 3: `src/Wpf/Navigation/PlaceholderNavTarget.cs` 작성**

```csharp
namespace CanMonitor.Wpf.Navigation;

public sealed class PlaceholderNavTarget : INavTarget
{
    public PlaceholderNavTarget(string key, string iconGlyph, string title, int order)
    {
        Key = key;
        IconGlyph = iconGlyph;
        Title = title;
        Order = order;
    }

    public string Key { get; }
    public string Title { get; }
    public string IconGlyph { get; }
    public int Order { get; }

    public object CreateViewModel(IServiceProvider sp) => new PlaceholderViewModel(Title);
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~PlaceholderNavTargetTests`
Expected: `Passed: 2, Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add src/Wpf/Navigation/PlaceholderNavTarget.cs tests/Wpf.Tests/Navigation
git commit -m "feat(wpf): add PlaceholderNavTarget for stub tabs"
```

---

## Task 8: Dashboard 인프라 — `IDashboardWidget`, `PlaceholderWidget`, `PlaceholderWidgetView`

**목표:** 대시보드 WrapPanel 안에 들어가는 위젯 3개의 데이터 타입과 placeholder View. DashboardViewModel 은 Task 9 에서.

**Files:**
- Create: `src/Wpf/Dashboard/IDashboardWidget.cs`
- Create: `src/Wpf/Dashboard/Widgets/PlaceholderWidget.cs`
- Create: `src/Wpf/Dashboard/Widgets/PlaceholderWidgetViewModel.cs`
- Create: `src/Wpf/Dashboard/Widgets/PlaceholderWidgetView.xaml`
- Create: `src/Wpf/Dashboard/Widgets/PlaceholderWidgetView.xaml.cs`

- [ ] **Step 1: `src/Wpf/Dashboard/IDashboardWidget.cs`**

```csharp
namespace CanMonitor.Wpf.Dashboard;

public interface IDashboardWidget
{
    string Title { get; }
    int PreferredWidth { get; }
    int PreferredHeight { get; }
    object ViewModel { get; }
}
```

- [ ] **Step 2: `src/Wpf/Dashboard/Widgets/PlaceholderWidgetViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Dashboard.Widgets;

public sealed partial class PlaceholderWidgetViewModel : ObservableObject
{
    public PlaceholderWidgetViewModel(string title)
    {
        Title = title;
    }

    public string Title { get; }
    public string Message => $"{Title} — Phase 3b 구현 예정";
}
```

- [ ] **Step 3: `src/Wpf/Dashboard/Widgets/PlaceholderWidget.cs`**

```csharp
namespace CanMonitor.Wpf.Dashboard.Widgets;

public sealed class PlaceholderWidget : IDashboardWidget
{
    public PlaceholderWidget(string title, int preferredWidth, int preferredHeight)
    {
        Title = title;
        PreferredWidth = preferredWidth;
        PreferredHeight = preferredHeight;
        ViewModel = new PlaceholderWidgetViewModel(title);
    }

    public string Title { get; }
    public int PreferredWidth { get; }
    public int PreferredHeight { get; }
    public object ViewModel { get; }
}
```

- [ ] **Step 4: `src/Wpf/Dashboard/Widgets/PlaceholderWidgetView.xaml`**

```xml
<UserControl x:Class="CanMonitor.Wpf.Dashboard.Widgets.PlaceholderWidgetView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
            <TextBlock Text="{Binding Title}"
                       Style="{StaticResource BodyTextStyle}"
                       FontSize="16" FontWeight="SemiBold"
                       HorizontalAlignment="Center"/>
            <TextBlock Text="{Binding Message}"
                       Foreground="{DynamicResource TextSecondaryBrush}"
                       Margin="0,4,0,0"
                       HorizontalAlignment="Center"/>
        </StackPanel>
    </Grid>
</UserControl>
```

- [ ] **Step 5: `src/Wpf/Dashboard/Widgets/PlaceholderWidgetView.xaml.cs`**

```csharp
using System.Windows.Controls;

namespace CanMonitor.Wpf.Dashboard.Widgets;

public partial class PlaceholderWidgetView : UserControl
{
    public PlaceholderWidgetView() => InitializeComponent();
}
```

- [ ] **Step 6: 빌드 확인**

Run: `dotnet build src\Wpf\CanMonitor.Wpf.csproj`
Expected: 0 errors

- [ ] **Step 7: Commit**

```bash
git add src/Wpf/Dashboard
git commit -m "feat(wpf): add IDashboardWidget contract and PlaceholderWidget"
```

---

## Task 9: `DashboardViewModel` + `DashboardView` + 테스트

**목표:** DI 로 주입된 `IEnumerable<IDashboardWidget>` 을 `Widgets` 컬렉션으로 노출. 뷰는 `ScrollViewer` + `ItemsControl` with `WrapPanel`.

**Files:**
- Create: `src/Wpf/Dashboard/DashboardViewModel.cs`
- Create: `src/Wpf/Dashboard/DashboardView.xaml`
- Create: `src/Wpf/Dashboard/DashboardView.xaml.cs`
- Create: `tests/Wpf.Tests/Dashboard/DashboardViewModelTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using CanMonitor.Wpf.Dashboard;
using CanMonitor.Wpf.Dashboard.Widgets;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Dashboard;

public class DashboardViewModelTests
{
    [Fact]
    public void Widgets_preserves_registration_order()
    {
        var a = new PlaceholderWidget("A", 100, 100);
        var b = new PlaceholderWidget("B", 100, 100);
        var c = new PlaceholderWidget("C", 100, 100);

        var vm = new DashboardViewModel(new[] { a, b, c });

        vm.Widgets.Should().Equal(a, b, c);
    }

    [Fact]
    public void Widgets_is_empty_when_no_registrations()
    {
        var vm = new DashboardViewModel(Array.Empty<IDashboardWidget>());
        vm.Widgets.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~DashboardViewModelTests`
Expected: 빌드 실패

- [ ] **Step 3: `src/Wpf/Dashboard/DashboardViewModel.cs` 작성**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Dashboard;

public sealed partial class DashboardViewModel : ObservableObject
{
    public DashboardViewModel(IEnumerable<IDashboardWidget> widgets)
    {
        Widgets = widgets.ToList();
    }

    public IReadOnlyList<IDashboardWidget> Widgets { get; }
}
```

- [ ] **Step 4: `src/Wpf/Dashboard/DashboardView.xaml`**

```xml
<UserControl x:Class="CanMonitor.Wpf.Dashboard.DashboardView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="8">
        <ItemsControl ItemsSource="{Binding Widgets}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <WrapPanel Orientation="Horizontal"/>
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Border Margin="8" BorderThickness="1"
                            BorderBrush="{DynamicResource BorderBrush}"
                            Background="{DynamicResource SurfaceBrush}"
                            Width="{Binding PreferredWidth}"
                            Height="{Binding PreferredHeight}">
                        <DockPanel>
                            <TextBlock DockPanel.Dock="Top"
                                       Text="{Binding Title}"
                                       Style="{StaticResource BodyTextStyle}"
                                       FontWeight="SemiBold"
                                       Margin="10,8,10,4"/>
                            <ContentControl Content="{Binding ViewModel}" Margin="6"/>
                        </DockPanel>
                    </Border>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 5: `src/Wpf/Dashboard/DashboardView.xaml.cs`**

```csharp
using System.Windows.Controls;

namespace CanMonitor.Wpf.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView() => InitializeComponent();
}
```

- [ ] **Step 6: 테스트 실행 — 통과 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~DashboardViewModelTests`
Expected: `Passed: 2`

- [ ] **Step 7: Commit**

```bash
git add src/Wpf/Dashboard tests/Wpf.Tests/Dashboard
git commit -m "feat(wpf): add DashboardViewModel and WrapPanel view"
```

---

## Task 10: `DashboardNavTarget`

**목표:** Dashboard 탭을 나타내는 `INavTarget` 구현체. DI `IServiceProvider` 에서 `DashboardViewModel` 싱글톤을 꺼내 반환.

**Files:**
- Create: `src/Wpf/Navigation/DashboardNavTarget.cs`
- Create: `tests/Wpf.Tests/Navigation/DashboardNavTargetTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using CanMonitor.Wpf.Dashboard;
using CanMonitor.Wpf.Navigation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanMonitor.Wpf.Tests.Navigation;

public class DashboardNavTargetTests
{
    [Fact]
    public void Properties_are_fixed()
    {
        var target = new DashboardNavTarget();
        target.Key.Should().Be("Dashboard");
        target.Title.Should().Be("Dashboard");
        target.IconGlyph.Should().Be("BarChart4");
        target.Order.Should().Be(10);
    }

    [Fact]
    public void CreateViewModel_resolves_DashboardViewModel_from_sp()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DashboardViewModel(Array.Empty<IDashboardWidget>()));
        var sp = services.BuildServiceProvider();

        var target = new DashboardNavTarget();
        var vm = target.CreateViewModel(sp);

        vm.Should().BeOfType<DashboardViewModel>();
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~DashboardNavTargetTests`
Expected: 빌드 실패

- [ ] **Step 3: `src/Wpf/Navigation/DashboardNavTarget.cs`**

```csharp
using CanMonitor.Wpf.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace CanMonitor.Wpf.Navigation;

public sealed class DashboardNavTarget : INavTarget
{
    public string Key => "Dashboard";
    public string Title => "Dashboard";
    public string IconGlyph => "BarChart4";
    public int Order => 10;

    public object CreateViewModel(IServiceProvider sp) => sp.GetRequiredService<DashboardViewModel>();
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~DashboardNavTargetTests`
Expected: `Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/Wpf/Navigation/DashboardNavTarget.cs tests/Wpf.Tests/Navigation/DashboardNavTargetTests.cs
git commit -m "feat(wpf): add DashboardNavTarget (Order=10)"
```

---

## Task 11: `StatusBarViewModel` + 테스트 (TDD, TestScheduler)

**목표:** CanEventHub 의 Frames/Signals/Alarms 스트림과 IAlarmEngine.CurrentAlarms, RawFrameStore.DroppedCount, SessionViewModel 의 StateChanges/DbcChanges 를 구독하여 Rx/s · Tx/s · Drop · DecodeFail · Alarms · DBC 레이블을 노출. UI 스케줄러는 DI 로 주입.

**Files:**
- Create: `src/Wpf/Shell/StatusBarViewModel.cs`
- Create: `tests/Wpf.Tests/Shell/StatusBarViewModelTests.cs`

**Note:** 테스트에서는 `SessionViewModel` 대신 경량 인터페이스 `ISessionState` 를 쓴다. SessionViewModel 은 Task 12 에서 같은 인터페이스를 구현하도록 한다.

- [ ] **Step 1: `ISessionState` 인터페이스 추가 (Shell 폴더)**

```csharp
// src/Wpf/Shell/ISessionState.cs
using CanMonitor.Wpf.Infrastructure;

namespace CanMonitor.Wpf.Shell;

public interface ISessionState
{
    IObservable<ConnectionState> StateChanges { get; }
    IObservable<DbcFileOption?> DbcChanges { get; }
}
```

- [ ] **Step 2: 실패 테스트 — `tests/Wpf.Tests/Shell/StatusBarViewModelTests.cs`**

```csharp
using System.Reactive.Subjects;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using NSubstitute;  // if not available, instantiate real AlarmEngine stub
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class StatusBarViewModelTests
{
    private sealed class FakeSessionState : ISessionState
    {
        public readonly BehaviorSubject<ConnectionState> State = new(ConnectionState.Disconnected);
        public readonly BehaviorSubject<DbcFileOption?> Dbc = new(null);
        public IObservable<ConnectionState> StateChanges => State;
        public IObservable<DbcFileOption?> DbcChanges => Dbc;
    }

    private sealed class FakeAlarmEngine : IAlarmEngine
    {
        public readonly Subject<AlarmState> Changes = new();
        public IObservable<AlarmState> AlarmChanges => Changes;
        public IReadOnlyCollection<AlarmState> CurrentAlarms { get; set; } = Array.Empty<AlarmState>();
        public void Submit(SignalValue value) { }
    }

    private static CanFrame MakeFrame(CanDirection dir)
        => new(0x100, false, ReadOnlyMemory<byte>.Empty, DateTimeOffset.UtcNow, dir);

    [Fact]
    public void RxPerSecond_counts_Rx_frames_over_one_second()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var frames = new Subject<CanFrame>();
        hub.AttachRawStreams(frames, Observable.Never<SignalValue>(),
            Observable.Never<AlarmState>(), Observable.Never<BusStatusChange>());
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();

        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        frames.OnNext(MakeFrame(CanDirection.Rx));
        frames.OnNext(MakeFrame(CanDirection.Rx));
        frames.OnNext(MakeFrame(CanDirection.Tx));

        sched.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        vm.RxPerSecond.Should().Be(2);
        vm.TxPerSecond.Should().Be(1);
    }

    [Fact]
    public void DroppedFrames_reflects_store_counter_after_sample()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore(capacity: 1);
        store.Record(MakeFrame(CanDirection.Rx));
        store.Record(MakeFrame(CanDirection.Rx));  // drops 1
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();

        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        sched.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        vm.DroppedFrames.Should().Be(1);
    }

    [Fact]
    public void ActiveAlarms_counts_Active_state_transitions()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();
        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        alarms.Changes.OnNext(new AlarmState("A1", AlarmSeverity.Warning, "x", true, DateTimeOffset.UtcNow));
        alarms.Changes.OnNext(new AlarmState("A2", AlarmSeverity.Critical, "y", true, DateTimeOffset.UtcNow));
        alarms.Changes.OnNext(new AlarmState("A1", AlarmSeverity.Warning, "x", false, DateTimeOffset.UtcNow));

        sched.AdvanceBy(1);  // drain scheduled actions
        vm.ActiveAlarms.Should().Be(1);
    }

    [Fact]
    public void Session_updates_on_state_change()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();
        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        session.State.OnNext(ConnectionState.Connected);
        sched.AdvanceBy(1);

        vm.Session.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public void DbcFileLabel_is_empty_when_no_selection()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();
        var vm = new StatusBarViewModel(hub, store, alarms, session, sched);

        session.Dbc.OnNext(new DbcFileOption("x/y.dbc", "120HP_NoPto.dbc", DbcSource.Confirmed));
        sched.AdvanceBy(1);

        vm.DbcFileLabel.Should().Be("120HP_NoPto.dbc");
    }
}
```

(NSubstitute 는 쓰지 않음 — 위 테스트는 Fake 구현만 사용하므로 import 줄 제거 후 제출할 것.)

- [ ] **Step 3: 테스트 실행 — 실패 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~StatusBarViewModelTests`
Expected: 빌드 실패 (`StatusBarViewModel` / `ISessionState` 미정의)

- [ ] **Step 4: `src/Wpf/Shell/StatusBarViewModel.cs` 작성**

```csharp
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Shell;

public sealed partial class StatusBarViewModel : ObservableObject, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();

    [ObservableProperty] private int _rxPerSecond;
    [ObservableProperty] private int _txPerSecond;
    [ObservableProperty] private long _droppedFrames;
    [ObservableProperty] private long _decodeFailures;
    [ObservableProperty] private int _activeAlarms;
    [ObservableProperty] private string _dbcFileLabel = string.Empty;
    [ObservableProperty] private ConnectionState _session;

    public StatusBarViewModel(
        CanEventHub hub,
        RawFrameStore store,
        IAlarmEngine alarmEngine,
        ISessionState sessionState,
        IScheduler uiScheduler)
    {
        var window = TimeSpan.FromSeconds(1);

        _subscriptions.Add(hub.Frames
            .Where(f => f.Direction == CanDirection.Rx)
            .Buffer(window, uiScheduler)
            .ObserveOn(uiScheduler)
            .Subscribe(batch => RxPerSecond = batch.Count));

        _subscriptions.Add(hub.Frames
            .Where(f => f.Direction == CanDirection.Tx)
            .Buffer(window, uiScheduler)
            .ObserveOn(uiScheduler)
            .Subscribe(batch => TxPerSecond = batch.Count));

        _subscriptions.Add(Observable
            .Interval(window, uiScheduler)
            .Select(_ => store.DroppedCount)
            .ObserveOn(uiScheduler)
            .Subscribe(v => DroppedFrames = v));

        // DecodeFailures: SignalValue에 Status 속성이 없어 Phase 3a에서는 0으로 유지.
        // Phase 3b에서 SignalValue에 Status 필드 추가 후 해당 Where+Scan 블록 복구.

        var initialActive = alarmEngine.CurrentAlarms.Count(a => a.Active);
        ActiveAlarms = initialActive;
        _subscriptions.Add(alarmEngine.AlarmChanges
            .Scan(initialActive, (count, alarm) => alarm.Active ? count + 1 : Math.Max(count - 1, 0))
            .ObserveOn(uiScheduler)
            .Subscribe(v => ActiveAlarms = v));

        _subscriptions.Add(sessionState.StateChanges
            .ObserveOn(uiScheduler)
            .Subscribe(s => Session = s));

        _subscriptions.Add(sessionState.DbcChanges
            .Select(d => d?.DisplayName ?? string.Empty)
            .ObserveOn(uiScheduler)
            .Subscribe(label => DbcFileLabel = label));
    }

    public void Dispose() => _subscriptions.Dispose();
}
```

**결정:** `SignalValue` 는 `(MessageName, SignalName, RawValue, PhysicalValue, Unit, Timestamp)` 만 노출하고 `Status` 필드가 없다. Phase 3a 는 `DecodeFailures = 0` 으로 유지 (VM 구독 없음). 테스트도 해당 케이스를 넣지 않는다. Phase 3b 에서 `SignalValue` 스키마 확장 후 복구.

- [ ] **Step 5: 테스트 실행 — 통과 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~StatusBarViewModelTests`
Expected: `Passed: 4`

- [ ] **Step 6: Commit**

```bash
git add src/Wpf/Shell/ISessionState.cs src/Wpf/Shell/StatusBarViewModel.cs tests/Wpf.Tests/Shell
git commit -m "feat(wpf): add StatusBarViewModel with Rx/Tx/Alarm/Drop subscriptions"
```

---

## Task 12: `SessionViewModel` + 테스트 (TDD)

**목표:** DBC 폴더 스캔, 기본 로드, Connect/Disconnect 명령, ManualBusStatusPublisher 에 상태 전이 브로드캐스트. `ISessionState` 구현.

**Files:**
- Create: `src/Wpf/Shell/SessionViewModel.cs`
- Create: `tests/Wpf.Tests/Shell/SessionViewModelTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using System.IO;
using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class SessionViewModelTests
{
    private sealed class FakeDbcProvider : IDbcProvider
    {
        public DbcDatabase Current { get; private set; } = DbcDatabase.Empty;
        public event EventHandler<DbcDatabase>? DatabaseReplaced;
        public bool ShouldFail { get; set; }
        public List<string> LoadedPaths { get; } = new();

        public Task LoadAsync(string path, CancellationToken ct = default)
        {
            LoadedPaths.Add(path);
            if (ShouldFail) throw new InvalidOperationException("fake failure");
            Current = new DbcDatabase(Array.Empty<DbcMessage>());
            DatabaseReplaced?.Invoke(this, Current);
            return Task.CompletedTask;
        }

        public Task SaveAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static string CreateTempDbc(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "VERSION \"\"\n");
        return path;
    }

    private sealed class FakeAlarmEngine : IAlarmEngine
    {
        public IObservable<AlarmState> AlarmChanges => Observable.Never<AlarmState>();
        public IReadOnlyCollection<AlarmState> CurrentAlarms => Array.Empty<AlarmState>();
        public void Submit(SignalValue value) { }
    }

    private static SessionViewModel CreateVm(string rootDir, FakeDbcProvider dbc, CanBusFactory factory,
        CanEventHub hub, ManualBusStatusPublisher publisher)
        => new SessionViewModel(factory, dbc, hub, publisher,
            new FakeAlarmEngine(), Array.Empty<IBusHeartbeatProvider>(), rootDir);

    [Fact]
    public async Task InitializeAsync_loads_default_dbc_when_present()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);

        await vm.InitializeAsync();

        vm.DbcFiles.Should().ContainSingle(f => f.DisplayName == "120HP_NoPto.dbc");
        vm.SelectedDbc.Should().NotBeNull();
        dbc.LoadedPaths.Should().ContainSingle();
        vm.State.Should().Be(ConnectionState.Disconnected);
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task InitializeAsync_sets_error_on_load_failure()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider { ShouldFail = true };
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);

        await vm.InitializeAsync();

        vm.State.Should().Be(ConnectionState.Error);
        vm.ErrorMessage.Should().Contain("fake failure");
    }

    [Fact]
    public async Task ConnectCommand_publishes_Connected_status()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);
        await vm.InitializeAsync();

        BusStatusChange? lastStatus = null;
        using var sub = pub.Changes.Subscribe(c => lastStatus = c);

        await vm.ConnectCommand.ExecuteAsync(null);

        vm.State.Should().Be(ConnectionState.Connected);
        lastStatus!.Status.Should().Be(BusStatus.Connected);
    }

    [Fact]
    public async Task DisconnectAsync_publishes_Disconnected_and_is_idempotent()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);
        await vm.InitializeAsync();
        await vm.ConnectCommand.ExecuteAsync(null);

        await vm.DisconnectAsync();
        vm.State.Should().Be(ConnectionState.Disconnected);

        await vm.DisconnectAsync();  // second call must be no-op
        vm.State.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task StateChanges_emits_on_transitions()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-ssn-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var pub = new ManualBusStatusPublisher();
        var factory = new CanBusFactory();
        var vm = CreateVm(tmp, dbc, factory, hub, pub);
        await vm.InitializeAsync();

        var emitted = new List<ConnectionState>();
        using var sub = vm.StateChanges.Subscribe(s => emitted.Add(s));

        await vm.ConnectCommand.ExecuteAsync(null);
        await vm.DisconnectAsync();

        emitted.Should().ContainInOrder(
            ConnectionState.Disconnected,  // BehaviorSubject replay
            ConnectionState.Connecting,
            ConnectionState.Connected,
            ConnectionState.Disconnected);
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~SessionViewModelTests`
Expected: 빌드 실패

- [ ] **Step 3: `src/Wpf/Shell/SessionViewModel.cs` 작성**

```csharp
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanMonitor.Wpf.Shell;

public sealed partial class SessionViewModel : ObservableObject, ISessionState, IDisposable
{
    private readonly ICanBusFactory _factory;
    private readonly IDbcProvider _dbcProvider;
    private readonly CanEventHub _hub;
    private readonly ManualBusStatusPublisher _publisher;
    private readonly IAlarmEngine _alarmEngine;
    private readonly IEnumerable<IBusHeartbeatProvider> _heartbeatProviders;
    private readonly string _dbcRootDir;
    private readonly BehaviorSubject<ConnectionState> _stateSubject = new(ConnectionState.Disconnected);
    private readonly BehaviorSubject<DbcFileOption?> _dbcSubject = new(null);

    private ICanBus? _currentBus;
    private TxScheduler? _currentTxScheduler;
    private BusLifecycleService? _currentLifecycle;
    private IDisposable? _hubBinding;

    [ObservableProperty] private ConnectionState _state = ConnectionState.Disconnected;
    [ObservableProperty] private string? _errorMessage;

    public SessionViewModel(
        ICanBusFactory factory,
        IDbcProvider dbcProvider,
        CanEventHub hub,
        ManualBusStatusPublisher publisher,
        IAlarmEngine alarmEngine,
        IEnumerable<IBusHeartbeatProvider> heartbeatProviders,
        string dbcRootDir = "dbc")
    {
        _factory = factory;
        _dbcProvider = dbcProvider;
        _hub = hub;
        _publisher = publisher;
        _alarmEngine = alarmEngine;
        _heartbeatProviders = heartbeatProviders;
        _dbcRootDir = dbcRootDir;

        Adapters = _factory.Known;
        SelectedAdapter = Adapters.First();
    }

    public IReadOnlyList<AdapterOption> Adapters { get; }
    public AdapterOption SelectedAdapter { get; set; }
    public IReadOnlyList<DbcFileOption> DbcFiles { get; private set; } = Array.Empty<DbcFileOption>();

    private DbcFileOption? _selectedDbc;
    public DbcFileOption? SelectedDbc
    {
        get => _selectedDbc;
        set
        {
            if (SetProperty(ref _selectedDbc, value))
                _dbcSubject.OnNext(value);
        }
    }

    public IObservable<ConnectionState> StateChanges => _stateSubject;
    public IObservable<DbcFileOption?> DbcChanges => _dbcSubject;

    public async Task InitializeAsync()
    {
        DbcFiles = ScanDbcFolder(_dbcRootDir);
        SelectedDbc = DbcFiles.FirstOrDefault(f => f.DisplayName.Equals("120HP_NoPto.dbc", StringComparison.OrdinalIgnoreCase))
                      ?? DbcFiles.FirstOrDefault();
        if (SelectedDbc is null) return;
        try
        {
            await _dbcProvider.LoadAsync(SelectedDbc.Path);
        }
        catch (Exception ex)
        {
            SetState(ConnectionState.Error);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting) return;
        try
        {
            SetState(ConnectionState.Connecting);
            _currentBus = _factory.Create(SelectedAdapter.Kind);
            await _currentBus.OpenAsync();
            _currentTxScheduler = new TxScheduler(_currentBus);
            _currentLifecycle = new BusLifecycleService(_heartbeatProviders, _currentTxScheduler);
            _hubBinding = _hub.Attach(_currentBus,
                Observable.Never<SignalValue>(),              // Phase 3b: SignalDecoder 연결
                _alarmEngine.AlarmChanges,
                _publisher.Changes);
            _currentLifecycle.Start();
            _publisher.Publish(new BusStatusChange(BusStatus.Connected, null, null, 0, DateTimeOffset.UtcNow));
            SetState(ConnectionState.Connected);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            SetState(ConnectionState.Error);
            ErrorMessage = ex.Message;
            await DisconnectAsync();
        }
    }

    [RelayCommand]
    private Task DisconnectCommandImpl() => DisconnectAsync();

    public async Task DisconnectAsync()
    {
        if (_currentBus is null && State == ConnectionState.Disconnected) return;
        try
        {
            if (_currentLifecycle is not null) await _currentLifecycle.DisposeAsync();
            if (_currentTxScheduler is not null) await _currentTxScheduler.DisposeAsync();
            _hubBinding?.Dispose();
            if (_currentBus is not null) await _currentBus.CloseAsync();
            _publisher.Publish(new BusStatusChange(BusStatus.Disconnected, null, null, 0, DateTimeOffset.UtcNow));
        }
        finally
        {
            _currentLifecycle = null;
            _currentTxScheduler = null;
            _hubBinding = null;
            _currentBus = null;
            SetState(ConnectionState.Disconnected);
        }
    }

    private void SetState(ConnectionState s)
    {
        State = s;
        _stateSubject.OnNext(s);
    }

    private static IReadOnlyList<DbcFileOption> ScanDbcFolder(string root)
    {
        var result = new List<DbcFileOption>();
        Add(Path.Combine(root, "confirmed"), DbcSource.Confirmed);
        Add(Path.Combine(root, "experimental"), DbcSource.Experimental);
        return result;

        void Add(string dir, DbcSource src)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var path in Directory.EnumerateFiles(dir, "*.dbc"))
                result.Add(new DbcFileOption(path, Path.GetFileName(path), src));
        }
    }

    public void Dispose()
    {
        _hubBinding?.Dispose();
        _stateSubject.Dispose();
        _dbcSubject.Dispose();
    }
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~SessionViewModelTests`
Expected: `Passed: 5`

- [ ] **Step 5: Commit**

```bash
git add src/Wpf/Shell/SessionViewModel.cs tests/Wpf.Tests/Shell/SessionViewModelTests.cs
git commit -m "feat(wpf): add SessionViewModel with DBC load + Connect/Disconnect flow"
```

---

## Task 13: `ShellViewModel` + 테스트

**목표:** `NavTargets` 를 Order 오름차순 정렬, Dashboard 기본 선택, `SelectedTarget` 변경 시 `CurrentViewModel` 교체.

**Files:**
- Create: `src/Wpf/Shell/ShellViewModel.cs`
- Create: `tests/Wpf.Tests/Shell/ShellViewModelTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using CanMonitor.Wpf.Dashboard;
using CanMonitor.Wpf.Navigation;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class ShellViewModelTests
{
    private static IServiceProvider BuildSp()
    {
        var svc = new ServiceCollection();
        svc.AddSingleton(new DashboardViewModel(Array.Empty<IDashboardWidget>()));
        return svc.BuildServiceProvider();
    }

    [Fact]
    public void NavTargets_sorted_by_Order_ascending()
    {
        var targets = new INavTarget[]
        {
            new PlaceholderNavTarget("Z", "icon", "Last", 70),
            new DashboardNavTarget(),
            new PlaceholderNavTarget("M", "icon", "Mid", 30)
        };

        var vm = new ShellViewModel(targets, BuildSp(),
            session: null!, status: null!);

        vm.NavTargets.Select(t => t.Order).Should().ContainInOrder(10, 30, 70);
    }

    [Fact]
    public void Default_selected_is_Dashboard()
    {
        var vm = new ShellViewModel(
            new INavTarget[] { new DashboardNavTarget(), new PlaceholderNavTarget("Raw", "icon", "Raw", 20) },
            BuildSp(), session: null!, status: null!);

        vm.SelectedTarget.Key.Should().Be("Dashboard");
        vm.CurrentViewModel.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public void Changing_SelectedTarget_updates_CurrentViewModel()
    {
        var vm = new ShellViewModel(
            new INavTarget[] { new DashboardNavTarget(), new PlaceholderNavTarget("Raw", "icon", "Raw", 20) },
            BuildSp(), session: null!, status: null!);

        vm.SelectedTarget = vm.NavTargets.First(t => t.Key == "Raw");

        vm.CurrentViewModel.Should().BeOfType<PlaceholderViewModel>();
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~ShellViewModelTests`
Expected: 빌드 실패

- [ ] **Step 3: `src/Wpf/Shell/ShellViewModel.cs`**

```csharp
using CanMonitor.Wpf.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Shell;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    public ShellViewModel(
        IEnumerable<INavTarget> targets,
        IServiceProvider sp,
        SessionViewModel session,
        StatusBarViewModel status)
    {
        _sp = sp;
        NavTargets = targets.OrderBy(t => t.Order).ToList();
        Session = session;
        Status = status;
        SelectedTarget = NavTargets.FirstOrDefault(t => t.Key == "Dashboard") ?? NavTargets[0];
    }

    public IReadOnlyList<INavTarget> NavTargets { get; }
    public SessionViewModel Session { get; }
    public StatusBarViewModel Status { get; }

    private INavTarget _selectedTarget = null!;
    public INavTarget SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value))
                CurrentViewModel = value.CreateViewModel(_sp);
        }
    }

    [ObservableProperty] private object _currentViewModel = new();
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인**

Run: `dotnet test tests\Wpf.Tests\CanMonitor.Wpf.Tests.csproj --filter FullyQualifiedName~ShellViewModelTests`
Expected: `Passed: 3`

- [ ] **Step 5: Commit**

```bash
git add src/Wpf/Shell/ShellViewModel.cs tests/Wpf.Tests/Shell/ShellViewModelTests.cs
git commit -m "feat(wpf): add ShellViewModel with nav target switching"
```

---

## Task 14: `ShellWindow.xaml` 레이아웃 + DataTemplate + `App.xaml.cs` DI 조립

**목표:** 상단 세션바 + 좌측 Rail + 중앙 Content + 하단 상태바 XAML 작성. App.xaml 에 DataTemplate 등록, App.xaml.cs 에서 Host Builder 로 전체 DI 조립.

**Files:**
- Modify: `src/Wpf/Shell/ShellWindow.xaml`
- Modify: `src/Wpf/App.xaml`
- Modify: `src/Wpf/App.xaml.cs`

- [ ] **Step 1: `src/Wpf/Shell/ShellWindow.xaml` — 최종 레이아웃**

```xml
<Window x:Class="CanMonitor.Wpf.Shell.ShellWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ctrl="clr-namespace:CanMonitor.Wpf.Controls"
        Title="CAN Monitor" Height="720" Width="1280"
        Background="{DynamicResource BackgroundBrush}">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Session bar -->
        <Border Grid.Row="0" Background="#1A2028" Padding="12,6">
            <DockPanel DataContext="{Binding Session}" LastChildFill="True">
                <TextBlock DockPanel.Dock="Left" Text="CAN Monitor"
                           Foreground="{DynamicResource RailForegroundBrush}"
                           FontWeight="SemiBold" VerticalAlignment="Center" Margin="0,0,18,0"/>
                <TextBlock DockPanel.Dock="Left" Text="Adapter:" Foreground="{DynamicResource RailForegroundBrush}"
                           VerticalAlignment="Center" Margin="0,0,6,0"/>
                <ComboBox DockPanel.Dock="Left" Width="120"
                          ItemsSource="{Binding Adapters}"
                          SelectedItem="{Binding SelectedAdapter}"
                          DisplayMemberPath="DisplayName" Margin="0,0,12,0"/>
                <TextBlock DockPanel.Dock="Left" Text="DBC:" Foreground="{DynamicResource RailForegroundBrush}"
                           VerticalAlignment="Center" Margin="0,0,6,0"/>
                <ComboBox DockPanel.Dock="Left" Width="220"
                          ItemsSource="{Binding DbcFiles}"
                          SelectedItem="{Binding SelectedDbc}"
                          DisplayMemberPath="DisplayName" Margin="0,0,12,0"/>
                <ctrl:LedIndicator DockPanel.Dock="Left" State="{Binding State}"
                                   VerticalAlignment="Center" Margin="0,0,6,0"/>
                <Button DockPanel.Dock="Right" Content="Disconnect"
                        Command="{Binding DisconnectCommand}" Margin="6,0,0,0"/>
                <Button DockPanel.Dock="Right" Content="Connect"
                        Command="{Binding ConnectCommand}"/>
                <TextBlock Text="{Binding ErrorMessage}" Foreground="{DynamicResource LedErrorBrush}"
                           VerticalAlignment="Center" Margin="8,0"/>
            </DockPanel>
        </Border>

        <!-- Main area: Rail + Content -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="56"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Rail -->
            <Border Grid.Column="0" Background="{DynamicResource RailBackgroundBrush}">
                <ItemsControl ItemsSource="{Binding NavTargets}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Button Command="{Binding DataContext.SelectTargetCommand,
                                              RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                    CommandParameter="{Binding}"
                                    Background="Transparent" BorderThickness="0"
                                    Height="56" ToolTip="{Binding Title}">
                                <StackPanel>
                                    <TextBlock Text="{Binding IconGlyph}" Style="{StaticResource IconGlyphStyle}"
                                               HorizontalAlignment="Center"/>
                                    <TextBlock Text="{Binding Title}" FontSize="9"
                                               Foreground="{DynamicResource RailForegroundBrush}"
                                               HorizontalAlignment="Center" Margin="0,2,0,0"/>
                                </StackPanel>
                            </Button>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </Border>

            <!-- Content -->
            <ContentControl Grid.Column="1" Content="{Binding CurrentViewModel}"/>
        </Grid>

        <!-- Status bar -->
        <Border Grid.Row="2" Background="#E3E7EE" BorderBrush="{DynamicResource BorderBrush}"
                BorderThickness="0,1,0,0" Padding="12,4">
            <StackPanel Orientation="Horizontal" DataContext="{Binding Status}">
                <StackPanel.Resources>
                    <Style TargetType="TextBlock" BasedOn="{StaticResource MonoTextStyle}">
                        <Setter Property="Margin" Value="0,0,18,0"/>
                    </Style>
                </StackPanel.Resources>
                <TextBlock>
                    <Run Text="●" Foreground="{DynamicResource LedConnectedBrush}"/>
                    <Run Text="{Binding Session}"/>
                </TextBlock>
                <TextBlock Text="{Binding RxPerSecond, StringFormat='Rx {0}/s'}"/>
                <TextBlock Text="{Binding TxPerSecond, StringFormat='Tx {0}/s'}"/>
                <TextBlock Text="{Binding DroppedFrames, StringFormat='Drop {0}'}"/>
                <TextBlock Text="{Binding DecodeFailures, StringFormat='DecodeFail {0}'}"/>
                <TextBlock Text="{Binding ActiveAlarms, StringFormat='Alarms {0}'}"/>
                <TextBlock Text="{Binding DbcFileLabel, StringFormat='DBC · {0}'}"
                           HorizontalAlignment="Right"/>
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

**Note on Rail Command:** `SelectTargetCommand` 가 ShellViewModel 에 없다. 가장 단순한 방법은 `SelectedTarget` 를 직접 바인딩하는 `ListBox` 를 쓰는 것. ItemsControl 이 아닌 `ListBox ItemsSource="{Binding NavTargets}" SelectedItem="{Binding SelectedTarget, Mode=TwoWay}"` 로 교체하고, ItemTemplate 에 Button 대신 Grid 를 쓰도록 `Step 1` 의 XAML 을 구현 시 수정한다. 테스트는 ViewModel 레벨에서 `SelectedTarget = ...` 대입으로 커버되므로 View 는 자유롭게 선택.

- [ ] **Step 2: `src/Wpf/App.xaml` 에 DataTemplate 추가**

```xml
<Application x:Class="CanMonitor.Wpf.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:dashVm="clr-namespace:CanMonitor.Wpf.Dashboard"
             xmlns:dashWidget="clr-namespace:CanMonitor.Wpf.Dashboard.Widgets"
             xmlns:navVm="clr-namespace:CanMonitor.Wpf.Navigation"
             xmlns:dashView="clr-namespace:CanMonitor.Wpf.Dashboard"
             xmlns:wView="clr-namespace:CanMonitor.Wpf.Dashboard.Widgets"
             xmlns:navView="clr-namespace:CanMonitor.Wpf.Navigation">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Themes/Light.xaml" />
                <ResourceDictionary Source="Themes/Styles/Typography.xaml" />
                <ResourceDictionary Source="Themes/Styles/Controls.xaml" />
            </ResourceDictionary.MergedDictionaries>

            <DataTemplate DataType="{x:Type dashVm:DashboardViewModel}">
                <dashView:DashboardView/>
            </DataTemplate>
            <DataTemplate DataType="{x:Type navVm:PlaceholderViewModel}">
                <navView:PlaceholderView/>
            </DataTemplate>
            <DataTemplate DataType="{x:Type wView:PlaceholderWidgetViewModel}">
                <wView:PlaceholderWidgetView/>
            </DataTemplate>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: `src/Wpf/App.xaml.cs` — 전체 DI 조립 + Host Builder**

```csharp
using System.Reactive.Concurrency;
using System.Windows;
using System.Windows.Threading;
using CanMonitor.Application;
using CanMonitor.Dbc;
using CanMonitor.Wpf.Dashboard;
using CanMonitor.Wpf.Dashboard.Widgets;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Navigation;
using CanMonitor.Wpf.Shell;
using CanMonitor.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanMonitor.Wpf;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var builder = Host.CreateApplicationBuilder();
        ConfigureServices(builder.Services);
        _host = builder.Build();
        await _host.StartAsync();

        var shell = _host.Services.GetRequiredService<ShellWindow>();
        shell.DataContext = _host.Services.GetRequiredService<ShellViewModel>();
        shell.Show();

        _ = _host.Services.GetRequiredService<SessionViewModel>().InitializeAsync();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services
            .AddCanMonitorApplication()
            .AddSingleton<IScheduler>(_ => new DispatcherScheduler(Application.Current.Dispatcher))
            .AddSingleton<IDbcProvider, DbcParserLibProvider>()
            .AddSingleton<ICanBusFactory, CanBusFactory>()
            .AddSingleton<ShellWindow>()
            .AddSingleton<ShellViewModel>()
            .AddSingleton<SessionViewModel>()
            .AddSingleton<ISessionState>(sp => sp.GetRequiredService<SessionViewModel>())
            .AddSingleton<StatusBarViewModel>()
            .AddSingleton<DashboardViewModel>()
            .AddSingleton<INavTarget, DashboardNavTarget>()                                         // Order=10
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Raw",       "BulletedList",   "Raw Log",         20))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Transmit",  "Send",           "Transmit",        30))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Test",      "TestBeaker",     "Test Runner",     40))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("DBC",       "Edit",           "DBC Editor",      50))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Input",     "GameController", "Input Emulation", 60))
            .AddSingleton<INavTarget>(_ => new PlaceholderNavTarget("Heartbeat", "Heart",          "Heartbeat",       70))
            .AddSingleton<IDashboardWidget>(_ => new PlaceholderWidget("Trend Chart",   620, 280))
            .AddSingleton<IDashboardWidget>(_ => new PlaceholderWidget("Signal Values", 400, 280))
            .AddSingleton<IDashboardWidget>(_ => new PlaceholderWidget("Alarm Panel",   1040, 220));
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try
            {
                var session = _host.Services.GetRequiredService<SessionViewModel>();
                await session.DisconnectAsync();
            }
            finally
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
        base.OnExit(e);
    }
}
```

**Note on `IDbcProvider` 등록:** `DbcParserLibProvider` 가 singleton 으로 바로 쓸 수 있는지(생성자 인자 확인) 를 Task 착수 시 점검. 기존 테스트 코드가 어떻게 주입하는지 `tests/Dbc.Tests/` 를 참고해 동일 방식으로 등록.

**Note on `DispatcherScheduler`:** `System.Reactive` 의 `DispatcherScheduler` 는 `Application.Current.Dispatcher` 필요. 위 코드는 `ConfigureServices` 실행 시점이 UI 스레드여야 함을 전제 — `OnStartup` 내부이므로 안전.

- [ ] **Step 4: 빌드 + 전체 테스트**

Run: `dotnet build CanMonitor.sln` then `dotnet test CanMonitor.sln`
Expected: 0 errors, 전체 기존 통과 + Wpf.Tests 신규 통과

- [ ] **Step 5: Commit**

```bash
git add src/Wpf/Shell/ShellWindow.xaml src/Wpf/App.xaml src/Wpf/App.xaml.cs
git commit -m "feat(wpf): wire DI host, ShellWindow layout, and DataTemplate map"
```

---

## Task 15: 수동 검증 + 계획 파일 커밋

**목표:** 스펙 §5.3 의 7단계 UI 수동 검증 체크리스트를 실제로 돌려보고, 모두 녹색이면 구현 완료. 마지막 커밋에 plan 파일 자체를 포함.

**Files:**
- Modify: 필요 시 버그 수정만
- Add (커밋 대상): `docs/superpowers/plans/2026-04-22-phase3a-shell-skeleton.md`

- [ ] **Step 1: 앱 실행**

Run: `dotnet run --project src\Wpf\CanMonitor.Wpf.csproj`
Expected: ShellWindow 출현

- [ ] **Step 2: 좌측 Rail 확인**

Expected: 아이콘 7개, 기본 선택 = Dashboard, Dashboard View 에 WrapPanel 위젯 3개 (Trend/Signal/Alarm placeholder) 가 보임

- [ ] **Step 3: 상단 바 드롭다운 확인**

Expected: Adapter = "Virtual", DBC 드롭다운에 `confirmed/120HP_NoPto.dbc` 포함, 기본 선택됨

- [ ] **Step 4: Connect 클릭**

Expected: LED 녹색, 하단 Rx/s 가 약 10 (EEC1 100 ms 주기) 근처에서 갱신, Tx/s 도 VirtualInputHeartbeat 주기에 따라 변동

- [ ] **Step 5: 다른 탭 클릭**

Expected: Raw / Transmit / Test / DBC / Input / Heartbeat 탭 클릭 시 "<Title> — 구현 예정 (Phase 3b 이후)." Placeholder 표시. 상태바의 Rx/s 는 여전히 흐름.

- [ ] **Step 6: Disconnect 클릭**

Expected: LED 회색, Rx/s · Tx/s 가 0 으로 수렴, Alarms 카운트 변화 없음

- [ ] **Step 7: 창 너비 축소**

Expected: Dashboard WrapPanel 위젯이 다음 줄로 wrap

- [ ] **Step 8: 앱 종료**

Expected: 예외 없음 (OnExit 에서 정상 DisconnectAsync → host.StopAsync)

- [ ] **Step 9: 계획 파일 커밋**

```bash
git add docs/superpowers/plans/2026-04-22-phase3a-shell-skeleton.md
git commit -m "docs: add Phase 3a WPF shell implementation plan"
```

- [ ] **Step 10: 최종 전체 테스트**

Run: `dotnet test CanMonitor.sln`
Expected: 이전 세션의 108 + Wpf.Tests 추가분 모두 통과, Failed: 0

---

## 위험 / 실행 시 확인할 것

- **`ITxScheduler` DI 등록 (dead)** — `AddCanMonitorApplication()` 은 `services.AddSingleton<ITxScheduler, TxScheduler>()` 를 등록하지만 `TxScheduler(ICanBus, IScheduler?)` 의 `ICanBus` 를 DI 에 등록한 코드가 없다. Phase 3a 는 `SessionViewModel.ConnectAsync` 에서 `TxScheduler` 와 `BusLifecycleService` 를 직접 `new` 로 조립 (Task 12 의 구현이 이미 그렇게 함). `ITxScheduler` 와 `BusLifecycleService` DI 등록은 Phase 3a 범위 내에서 `GetRequiredService` 호출을 하지 않으므로 실행시 문제 없음. 다만 후속 Phase 에서 `Host.Build()` 의 `ValidateOnBuild=true` 를 켜고 싶다면 이 두 등록을 Application 에서 제거해야 함 — Phase 3b 미결 항목.
- **`DispatcherScheduler` 생성자** — `System.Reactive` 6.0.1 에 `DispatcherScheduler(Dispatcher)` 공개 생성자 존재. 만약 환경에 따라 `Current` 정적 속성만 노출된다면 `DispatcherScheduler.Current` 로 대체. `App.OnStartup` 내부는 UI 스레드이므로 안전.
- **Rail 선택 UX** — 스펙에서 "ListBox/ItemsControl" 구체 결정 없음. 구현자는 `ListBox` + `SelectedItem="{Binding SelectedTarget, Mode=TwoWay}"` 양방향 바인딩을 채택. `ShellViewModel.SelectedTarget` setter 는 테스트로 이미 커버.
- **DBC 드롭다운 변경 시 재로드** — Task 12 의 `SessionViewModel.SelectedDbc` setter 는 현재 `_dbcSubject.OnNext` 만 호출. 스펙 §2.1 "SelectedDbc 변경 이벤트 구독 → 자동 재로드" 를 만족하려면 setter 에서 `_ = _dbcProvider.LoadAsync(value.Path)` fire-and-forget 호출 추가. Task 12 구현 시 이 한 줄을 포함하고, 단위 테스트 한 케이스(`SelectedDbc 변경 시 LoadAsync 호출`) 추가.
- **Alarm 구독 backlog** — `_hub.Attach` 는 `_alarmEngine.AlarmChanges` 를 함께 attach 하지만 `AlarmEngine` 은 SignalValue 를 `Submit` 받아 구동된다. Phase 3a 는 SignalDecoder 연결이 없어 실제 alarm stream 이 활성화되지 않음. 수동 검증 Step 4 에서 `Alarms 0` 으로 남는 것이 정상.

---

## Self-Review 노트 (작성자 — 플랜 완료 후 확인)

- **Spec coverage:**
    - §결정 1~10: Task 1 (프로젝트), Task 4 (테마), Task 5 (LED), Task 6-10 (네비/대시보드), Task 11 (상태바), Task 12 (세션 + DBC + Connect), Task 13 (ShellVM), Task 14 (App/DI/XAML) 로 모두 매핑
    - §2.1 `InitializeAsync`, `DisconnectAsync`, `StateChanges/DbcChanges`: Task 12 테스트 5 케이스
    - §2.2 Rx/Tx 방향 카운트, Active 알람 스캔: Task 11 테스트
    - §2.3 `NavTargets` Order 정렬, 기본 Dashboard: Task 13 테스트
    - §3.1 DI 조립: Task 14 Step 3
    - §4.5 DashboardView WrapPanel: Task 9 Step 4
    - §5.3 UI 수동 검증 7단계: Task 15
- **Placeholder scan:** "TBD" · "TODO" · "implement later" 없음. 위험 섹션에만 구현 시 확인할 사실 나열.
- **Type consistency:**
    - `ConnectionState.Error` (Task 2) ↔ `SessionViewModel.State` (Task 12) ↔ `LedIndicator.State` (Task 5) ↔ `StatusBarViewModel.Session` (Task 11) 모두 동일 enum
    - `ICanBusFactory.Create(AdapterKind)` 시그니처 Task 3/14 간 일치
    - `INavTarget.{Key, Title, IconGlyph, Order, CreateViewModel}` Task 6/7/10/13 간 일치
    - `DashboardNavTarget.Order = 10` 스펙 §결정 7 과 Task 10 고정값 일치

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-22-phase3a-shell-skeleton.md`. Two execution options:

1. **Subagent-Driven (recommended)** - 각 Task 당 fresh subagent 를 띄워 구현 → 스펙 compliance 리뷰 → 코드 품질 리뷰 → 다음 Task. 15개 Task 가 모두 본 세션 내에서 돈다.
2. **Inline Execution** - 본 세션에서 executing-plans 스킬로 배치 + 체크포인트 방식.

사용자의 durable directive 에 의해 Subagent-Driven Development 가 기본값. 별도 지시 없으면 **Subagent-Driven** 으로 진행.
