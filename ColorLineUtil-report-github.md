# ColorLineUtil Old/New Version Benchmark Result

``` ini

BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.23430.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=6.0.400
  [Host]     : .NET 6.0.21 (6.0.2123.36311), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.21 (6.0.2123.36311), X64 RyuJIT AVX2


```

|                       Method |           Mean |        Error |       StdDev |    Gen0 |    Gen1 |    Gen2 | Allocated |
|----------------------------- |---------------:|-------------:|-------------:|--------:|--------:|--------:|----------:|
|      OldAnalyze_Short_Normal |     9,184.2 ns |     75.09 ns |     58.62 ns |  0.0458 |       - |       - |     288 B |
| OldAnalyze_Short_Complicated |    36,266.1 ns |    654.45 ns |    546.50 ns |  0.1221 |       - |       - |    1064 B |
|       OldAnalyze_Long_Normal | 3,922,097.1 ns | 35,819.58 ns | 33,505.66 ns | 54.6875 | 54.6875 | 54.6875 |  187365 B |
|   OldAnalyze_Long_Some_Color | 4,358,603.5 ns | 53,668.10 ns | 47,575.37 ns | 23.4375 |  7.8125 |       - |  191724 B |
|      NewAnalyze_Short_Normal |       634.6 ns |     12.19 ns |     10.18 ns |  0.0935 |       - |       - |     592 B |
| NewAnalyze_Short_Complicated |     2,260.0 ns |     23.19 ns |     19.36 ns |  0.2098 |       - |       - |    1328 B |
|       NewAnalyze_Long_Normal |   713,679.6 ns |  5,496.25 ns |  4,589.62 ns | 58.5938 | 58.5938 | 58.5938 |  187663 B |
|   NewAnalyze_Long_Some_Color |   656,456.8 ns |  5,685.30 ns |  4,747.49 ns | 30.2734 | 13.6719 |       - |  193216 B |

## Test Data

- Short_Color: `11:45:14 <<color=Blue>Info</color>> Hello World!`
- Short_Complicated: `11:45:14 <<color=Blue>Info</color>> <color=</color><color=Yellow>yelolow text</color><color=Yellow></color><-nothing text<color=Yellow><color=White><color=Blue><>></color></color></color>`
- Long_Normal:

  ```cs
  StringBuilder sb = new("11:45:14 <<color=Blue>Info</color>> ");
  for (int i = 0; i < 250; i++)
  {
      sb.Append("The joke here is that Navia's name starts with Na, while the Element Sodium has the symbol of Na. Clorinde's name starts with Cl, while the element Chlorine has the symbol Cl. When their names are placed together, they form NaCl, which is an ionic salt. As a result, the ship name of the 2 characters is NaCl, which is funny because that could also refer to the salt as well");
  }
  Long_Normal = sb.ToString();
  ```

- Long_Some_Color:

  ```cs
  StringBuilder sb = new("11:45:14 <<color=Blue>Info</color>> ");
  for (int i = 0; i < 250; i++)
  {
      if (i % 10 == 0) sb.Append("<color=Yellow>");
      sb.Append("The joke here is that Navia's name starts with Na, while the Element Sodium has the symbol of Na. Clorinde's name starts with Cl, while the element Chlorine has the symbol Cl. When their names are placed together, they form NaCl, which is an ionic salt. As a result, the ship name of the 2 characters is NaCl, which is funny because that could also refer to the salt as well");
      if (i % 10 == 0) sb.Append("</color>");
  }
  Long_Some_Color = sb.ToString();
  ```