import { afterEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router";
import AuthProvider from "./AuthProvider";
import { useAuth } from "./AuthContext";
import { instance } from "../api/apiCalls";
import LogoutBtn from "../components/LogoutBtn";

const originalAdapter = instance.defaults.adapter;
afterEach(() => {
  instance.defaults.adapter = originalAdapter;
  vi.useRealTimers();
});

function token(role = "Customer", seconds = 3600) {
  return btoa(JSON.stringify({ alg: "none" })) + "." +
    btoa(JSON.stringify({ id: "5", role, firstName: "Elek", exp: Math.floor(Date.now() / 1000) + seconds })) + ".test";
}
function Session() {
  const { user, isAuthenticated, hasSession, alert, logIn, register } = useAuth();
  const location = useLocation();
  return <>
    <p data-testid="identity">{isAuthenticated ? user.role : "Guest"}</p>
    <p data-testid="session">{hasSession ? "stored" : "empty"}</p>
    <p data-testid="location">{location.pathname + location.hash}</p>
    {alert && <p role="alert">{alert.message}</p>}
    <button onClick={() => logIn("testuser", "TesztJelszo123!")}>Belépés</button>
    <button onClick={() => register({ userName: "testuser" })}>Regisztráció</button>
    <LogoutBtn />
  </>;
}
function showSession() {
  return render(<MemoryRouter><AuthProvider><Session /></AuthProvider></MemoryRouter>);
}

it.each(["Customer", "Courier", "WarehouseOperator", "Admin"])("az érvényes %s munkamenetet visszatölti és ki lehet lépni", async (role) => {
  localStorage.setItem("csomagkezelo_token", token(role));
  showSession();
  expect(screen.getByTestId("identity")).toHaveTextContent(role);
  await userEvent.click(screen.getByRole("button", { name: "Kilépés" }));
  expect(screen.getByTestId("identity")).toHaveTextContent("Guest");
  expect(localStorage.getItem("csomagkezelo_token")).toBeNull();
});

it("a hibás tárolt munkamenethez a valódi kilépés gomb is megjelenik", async () => {
  localStorage.setItem("csomagkezelo_token", "invalid");
  showSession();
  expect(screen.getByTestId("identity")).toHaveTextContent("Guest");
  await userEvent.click(screen.getByRole("button", { name: "Kilépés" }));
  expect(screen.getByTestId("session")).toHaveTextContent("empty");
});

it("a nyitott oldalon is lejár a belépés, a token a tudatos kilépésig megmarad", () => {
  vi.useFakeTimers();
  const value = token("WarehouseOperator", 1);
  localStorage.setItem("csomagkezelo_token", value);
  showSession();
  expect(screen.getByTestId("identity")).toHaveTextContent("WarehouseOperator");
  act(() => vi.advanceTimersByTime(2000));
  expect(screen.getByTestId("identity")).toHaveTextContent("Guest");
  expect(localStorage.getItem("csomagkezelo_token")).toBe(value);
  fireEvent.click(screen.getByRole("button", { name: "Kilépés" }));
  expect(localStorage.getItem("csomagkezelo_token")).toBeNull();
});

it("a sikeres bejelentkezés eltárolja a tokent és frissíti a szerepkört", async () => {
  const value = token("Courier");
  instance.defaults.adapter = async (config) => ({
    data: { token: value }, status: 200, statusText: "OK", headers: {}, config,
  });
  showSession();
  await userEvent.click(screen.getByRole("button", { name: "Belépés" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Sikeres bejelentkezés");
  expect(screen.getByTestId("identity")).toHaveTextContent("Courier");
  expect(localStorage.getItem("csomagkezelo_token")).toBe(value);
});

it("hibás belépési válasz tokenjét nem tárolja el", async () => {
  instance.defaults.adapter = async (config) => ({
    data: { token: "invalid" }, status: 200, statusText: "OK", headers: {}, config,
  });
  showSession();
  await userEvent.click(screen.getByRole("button", { name: "Belépés" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Érvénytelen vagy lejárt");
  expect(localStorage.getItem("csomagkezelo_token")).toBeNull();
});

it("hibás jelszónál a szerver üzenetét mutatja", async () => {
  instance.defaults.adapter = () => Promise.reject({ response: { status: 401, data: "Hibás jelszó!" } });
  showSession();
  await userEvent.click(screen.getByRole("button", { name: "Belépés" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Hibás jelszó");
  expect(screen.getByTestId("identity")).toHaveTextContent("Guest");
});

it("regisztráció után a létező belépési szakaszra navigál", async () => {
  instance.defaults.adapter = async (config) => ({
    data: "Sikeres regisztráció!", status: 200, statusText: "OK", headers: {}, config,
  });
  showSession();
  await userEvent.click(screen.getByRole("button", { name: "Regisztráció" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Sikeres regisztráció");
  expect(screen.getByTestId("location")).toHaveTextContent("/#belepes");
});
