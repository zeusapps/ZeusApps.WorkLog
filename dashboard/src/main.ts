import { bootstrapApplication } from "@angular/platform-browser";
import { Component } from "@angular/core";
import { provideRouter } from "@angular/router";
import { App } from "./app";
@Component({ selector: "route-anchor", template: "" })
class RouteAnchor {}
bootstrapApplication(App, {
  providers: [
    provideRouter([
      { path: "", component: RouteAnchor },
      { path: "ongoing", component: RouteAnchor },
      { path: "history", component: RouteAnchor },
      { path: "**", redirectTo: "ongoing" },
    ]),
  ],
}).catch((error) => {
  document.body.textContent = "WorkLog could not start. " + String(error);
});
