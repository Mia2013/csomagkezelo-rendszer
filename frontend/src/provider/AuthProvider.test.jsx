import { expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import AuthProvider from "./AuthProvider";
import { useAuth } from "./AuthContext";

function SessionStatus() {
  const { isAuthenticated, hasSession, alert, logOut } = useAuth();
  return <>
    <p>{isAuthenticated ? "Bejelentkezve" : "Nincs érvényes belépés"}</p>
    {alert && <p>{alert.message}</p>}
    {hasSession && <button onClick={logOut}>Kilépés</button>}
  </>;
}

it.each(["invalid", "eyJhbGciOiJub25lIn0." + btoa(JSON.stringify({ exp: 1, role: "Customer" })) + ".test"])(
  "a hibás vagy lejárt munkamenetből csak tudatos kilépés után lesz vendégfeladás (%s)", async token => {
    localStorage.setItem("csomagkezelo_token", token);
    const user = userEvent.setup();
    render(<MemoryRouter><AuthProvider><SessionStatus /></AuthProvider></MemoryRouter>);
    expect(screen.getByText("Nincs érvényes belépés")).toBeInTheDocument();
    expect(localStorage.getItem("csomagkezelo_token")).toBe(token);
    await user.click(screen.getByRole("button", { name: "Kilépés" }));
    expect(localStorage.getItem("csomagkezelo_token")).toBeNull();
    expect(screen.queryByRole("button", { name: "Kilépés" })).not.toBeInTheDocument();
  });
