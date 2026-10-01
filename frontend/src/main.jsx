import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";
import "./index.css";
import Layout from "./pages/Layout.jsx";
import { allPages } from "./pages/pages.js";
import AuthProvider from "./provider/AuthProvider";

const router = createBrowserRouter([
  {
    element: (
      <AuthProvider>
        <Layout />
      </AuthProvider>
    ),
    children: allPages.map((p) => ({
      path: p.path,
      Component: p.component,
    })),
  },
]);

createRoot(document.getElementById("root")).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
);
