import { beforeEach, expect, it, vi } from "vitest";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useNavigate } from "react-router";
import TrackPackageForm from "../components/TrackPackageForm";
import StatusTimeline from "../components/StatusTimeline";
import PackageForm from "../components/PackageForm";
import { getData } from "../api/apiCalls";
import { useAuth } from "../provider/AuthContext";

vi.mock("../api/apiCalls", () => ({
  getData: vi.fn(), postData: vi.fn(),
  endpoints: { trackPackage: "Package/track", getUserById: "User", createPackage: "Package" },
}));
vi.mock("../provider/AuthContext", () => ({ useAuth: vi.fn() }));
beforeEach(() => {
  vi.resetAllMocks();
  useAuth.mockReturnValue({ isAuthenticated: false, user: { role: "Guest" }, setAlert: vi.fn() });
});
const first = "77011983-e714-47b2-9bbb-cb3058c81a2e";
const second = "88f4ba73-60a8-45a3-8d9d-e0169b61acba";
function result(status) {
  return { currentStatus: status, history: [{ status, changedAt: "2026-10-26T10:00:00Z" }] };
}
it.each([
  ["PreBooked", "Előjegyezve", 0],
  ["AssignedForPickup", "Felvételre kiosztva", 0],
  ["InTransitToWarehouse", "Úton a raktárba", 1],
  ["AtWarehouse", "Raktárban", 2],
  ["AssignedForDelivery", "Kiszállításra kiosztva", 3],
  ["OutForDelivery", "Kiszállítás alatt", 3],
  ["Delivered", "Kézbesítve", 4],
  ["FailedDelivery", "Sikertelen kézbesítés", 3],
])("%s állapot magyarul és a megfelelő lépésnél jelenik meg", (status, label, index) => {
  render(<StatusTimeline result={result(status)} />);
  expect(screen.getByRole("status")).toHaveTextContent(label);
  const steps = screen.getAllByRole("listitem");
  expect(steps[index]).toHaveAttribute("aria-current", "step");
  expect(steps.filter((step) => step.hasAttribute("aria-current"))).toHaveLength(1);
});

it("ugyanarra az azonosítóra hiba után újra lehet keresni", async () => {
  getData.mockRejectedValueOnce(new Error("Átmeneti hiba")).mockResolvedValueOnce(result("AtWarehouse"));
  render(<MemoryRouter initialEntries={["/?trackingNumber=" + first]}><TrackPackageForm /></MemoryRouter>);
  expect(await screen.findByRole("alert")).toHaveTextContent("Átmeneti hiba");
  await userEvent.click(screen.getByRole("button", { name: "Keresés" }));
  expect(await screen.findByRole("status")).toHaveTextContent("Raktárban");
  expect(getData).toHaveBeenCalledTimes(2);
});

function OtherPackage() {
  const navigate = useNavigate();
  return <button onClick={() => navigate("/?trackingNumber=" + second)}>Másik csomag</button>;
}
it("URL-váltásnál újra keres és a régi kérés késői válaszát eldobja", async () => {
  let completeFirst;
  getData.mockImplementation((endpoint, query) => query.trackingNumber === first
    ? new Promise((resolve) => { completeFirst = resolve; })
    : Promise.resolve(result("AtWarehouse")));
  render(<MemoryRouter initialEntries={["/?trackingNumber=" + first]}><TrackPackageForm /><OtherPackage /></MemoryRouter>);
  await userEvent.click(screen.getByRole("button", { name: "Másik csomag" }));
  expect(await screen.findByRole("status")).toHaveTextContent("Raktárban");
  await act(async () => completeFirst(result("Delivered")));
  expect(screen.getByRole("status")).toHaveTextContent("Raktárban");
  expect(screen.getByLabelText("Csomagazonosító")).toHaveValue(second);
});

it("felhasználóváltásnál a régi profil válasza nem írja felül az új feladót", async () => {
  let completeFirst;
  useAuth.mockReturnValue({ isAuthenticated: true, user: { id: "5" }, setAlert: vi.fn() });
  getData.mockImplementation((endpoint) => endpoint === "User/5"
    ? new Promise((resolve) => { completeFirst = resolve; })
    : Promise.resolve({ lastName: "Új", firstName: "Feladó", address: "Veszprém", phoneNumber: "+36301234567" }));
  const view = render(<MemoryRouter><PackageForm /></MemoryRouter>);
  useAuth.mockReturnValue({ isAuthenticated: true, user: { id: "6" }, setAlert: vi.fn() });
  view.rerender(<MemoryRouter><PackageForm /></MemoryRouter>);
  await waitFor(() => expect(screen.getByLabelText(/Feladó neve/)).toHaveValue("Új Feladó"));
  await act(async () => completeFirst({ lastName: "Régi", firstName: "Feladó", address: "Pápa" }));
  expect(screen.getByLabelText(/Feladó neve/)).toHaveValue("Új Feladó");
});
