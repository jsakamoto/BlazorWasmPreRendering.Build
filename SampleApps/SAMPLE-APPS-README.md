# Sample Apps

|                   |BlazorWasmApp0| BlazorWasmApp1   | BlazorWasmApp2
|-------------------|--------------|------------------|--------------
|Framework          | .NET 8.0     | .NET 8.0         | .NET 6.0
|Projects           |Client + RCLIb| Client           | Component + Client   
|Root element       | `<app>`      | `<div id='app'>` | `<app>`
|Used in E2E test   | ✅ Yes       | ✅ Yes          | ✅ Yes
|Page title         | `<PageTitle>`| `<Title>`        | -
|PWA                | ✅ Yes       | -                | -
|Has easter-egg     | -            | ✅ Yes           | -
|Deploy to GitHub Pages| -         | ✅ Yes           | -
|Brotli Loader      | ✅ Yes       | -                | -
|Has AngleSharp dependency| ✅Yes  | -                | -
|Localization       | ✅ Yes       | -                | -
|Lazy Load Assembly | ✅ Yes       | ✅ Yes           | -

## Appendix

### BlazorWasmApp0

- The razor class library which is referenced from the `BlazorWasmApp0` project has `AssemblyMetadata` attributes to install middleware packages `Middleware1` ver.1.0 and `Middleware2` ver.2.0.

### BlazorWasmApp2

- Consists from the component library project and the Blazor WebAssembly project.
  - The component libray project defines the interface.
  - The Blazor Wasm project implements that interface and register it to a DI container.
