# Sample Apps

## BlazorWasmApp0

- .NET 6.0
- The root component DOM element tag is `<app>`.
- It is referenced from the End-to-End test.


## BlazorWasmApp1

- .NET 5.0
- The root component DOM element tag is `<div id="app">`.
- It is deployed to the GitHub Pages by GitHub Actions script.

## BlazorWasmApp2

- .NET 5.0
- Consists from the component library project and the Blazor WebAssembly project.
  - The component libray project defines the interface.
  - The Blazor Wasm project implements that interface and register it to a DI container.
- It is referenced from the End-to-End test.
