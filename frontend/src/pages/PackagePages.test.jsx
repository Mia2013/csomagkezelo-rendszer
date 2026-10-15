import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import CreatePackage from "./CreatePackage";
import TrackPackage from "./TrackPackage";
import { getData, postData } from "../api/apiCalls";

vi.mock("../api/apiCalls", () => ({
  getData: vi.fn(), postData: vi.fn(),
  endpoints: { createPackage: "Package", trackPackage: "Package/track" },
}));

const number = "77011983-e714-47b2-9bbb-cb3058c81a2e";
beforeEach(() => { vi.resetAllMocks(); });

async function fillForm(user) {
  const fields = [
    [/Feladó neve/, "Teszt Feladó"], [/Feladó címe/, "Veszprém, Teszt utca 1."],
    [/Címzett neve/, "Teszt Címzett"], [/Címzett címe/, "Pápa, Teszt utca 2."],
    [/Csomag értéke/, "15000"],
    [/Tömeg/, "1200"], [/Felvétel napja/, "2026-12-15"],
  ];
  for (const [label, value] of fields) { const input = screen.getByLabelText(label); await user.click(input); if (input.type === "number" || input.type === "date") await user.type(input, value); else await user.paste(value); }
}

describe("Csomagelőjegyzés", () => {
  it("csak az UML mezőit kéri be, elküldi az űrlapot és megjeleníti a követési linket", async () => {
    const user = userEvent.setup();
    postData.mockResolvedValue({ trackingNumber: number });
    render(<MemoryRouter><CreatePackage /></MemoryRouter>);
    expect(screen.queryByLabelText(/Feladó telefonszáma/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Címzett telefonszáma/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Címzett e-mail-címe/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Felvételi idősáv/)).not.toBeInTheDocument();
    await fillForm(user);
    await user.click(screen.getByRole("button", { name: "Csomag előjegyzése" }));
    expect(await screen.findByText(number)).toBeInTheDocument();
    expect(postData).toHaveBeenCalledWith("Package", {
      senderName: "Teszt Feladó", senderAddress: "Veszprém, Teszt utca 1.",
      recipientName: "Teszt Címzett", recipientAddress: "Pápa, Teszt utca 2.",
      weight: 1200, value: 15000, size: "M", pickupDate: "2026-12-15",
    });
    expect(screen.getByRole("link", { name: "Csomag követése" })).toHaveAttribute("href", `/csomagkovetes?trackingNumber=${number}`);
  });

  it("hibánál megőrzi a mezőket és megjeleníti a mezőhibát", async () => {
    const user = userEvent.setup();
    postData.mockRejectedValue(Object.assign(new Error("Ellenőrizd az adatokat!"), { errors: { pickupDate: "Túl korai dátum." } }));
    render(<MemoryRouter><CreatePackage /></MemoryRouter>);
    await fillForm(user);
    await user.click(screen.getByRole("button", { name: "Csomag előjegyzése" }));
    expect(await screen.findByText("Túl korai dátum.")).toBeInTheDocument();
    expect(screen.getByLabelText(/Feladó neve/)).toHaveValue("Teszt Feladó");
    expect(screen.getByRole("button", { name: "Csomag előjegyzése" })).toBeEnabled();
  });

  it("mentés közben tiltja az ismételt beküldést", async () => {
    const user = userEvent.setup();
    let finish;
    postData.mockReturnValue(new Promise(resolve => { finish = resolve; }));
    render(<MemoryRouter><CreatePackage /></MemoryRouter>);
    await fillForm(user);
    await user.dblClick(screen.getByRole("button", { name: "Csomag előjegyzése" }));
    expect(screen.getByRole("button", { name: "Mentés folyamatban…" })).toBeDisabled();
    expect(postData).toHaveBeenCalledTimes(1);
    finish({ trackingNumber: number });
    await screen.findByText(number);
  });

  it("üres űrlapot nem küld el", async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><CreatePackage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: "Csomag előjegyzése" }));
    expect(postData).not.toHaveBeenCalled();
  });
});

describe("Nyilvános követés", () => {
  it("a linkből automatikusan lekérdez és magyar státuszt mutat", async () => {
    getData.mockResolvedValue({ trackingNumber: number, status: "PreBooked", changedAt: "2026-10-03T10:00:00Z" });
    render(<MemoryRouter initialEntries={[`/csomagkovetes?trackingNumber=${number}`]}><TrackPackage /></MemoryRouter>);
    expect(await screen.findByText("Előjegyezve")).toBeInTheDocument();
    expect(getData).toHaveBeenCalledWith("Package/track", { trackingNumber: number }, true);
  });

  it("üres keresésnél nem hívja a szervert", async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><TrackPackage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: "Keresés" }));
    expect(screen.getByText("Add meg a csomagazonosítót!")).toBeInTheDocument();
    expect(getData).not.toHaveBeenCalled();
  });

  it("ismeretlen azonosítónál a szerver üzenetét mutatja", async () => {
    const user = userEvent.setup();
    getData.mockRejectedValue(new Error("Ezzel az azonosítóval nem található csomag."));
    render(<MemoryRouter><TrackPackage /></MemoryRouter>);
    await user.type(screen.getByLabelText("Csomagazonosító"), number);
    await user.click(screen.getByRole("button", { name: "Keresés" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("nem található csomag");
    await waitFor(() => expect(screen.getByRole("button", { name: "Keresés" })).toBeEnabled());
  });
});

