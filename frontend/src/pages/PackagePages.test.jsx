import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import dayjs from "dayjs";
import CreatePackage from "./CreatePackage";
import Home from "./Home";
import { getData, postData } from "../api/apiCalls";
import { useAuth } from "../provider/AuthContext";

vi.mock("../api/apiCalls", () => ({
  getData: vi.fn(),
  postData: vi.fn(),
  endpoints: {
    createPackage: "Package",
    trackPackage: "Package/track",
    getUserById: "User",
  },
}));
vi.mock("../provider/AuthContext", () => ({ useAuth: vi.fn() }));

const number = "77011983-e714-47b2-9bbb-cb3058c81a2e";
const pickupDate = dayjs().add(10, "day").format("YYYY-MM-DD");
const guest = {
  isAuthenticated: false,
  user: { role: "Guest" },
  logIn: vi.fn(),
};
const customer = { isAuthenticated: true, user: { id: "5", role: "Customer" } };

beforeEach(() => {
  vi.resetAllMocks();
  useAuth.mockReturnValue({ ...guest, setAlert: vi.fn() });
});

async function fillForm(user) {
  const fields = [
    [/Feladó neve/, "Teszt Feladó"],
    [/Feladó címe/, "Veszprém, Teszt utca 1."],
    [/Feladó telefonszáma/, "+36301234567"],
    [/Címzett neve/, "Teszt Címzett"],
    [/Címzett címe/, "Pápa, Teszt utca 2."],
    [/Címzett telefonszáma/, "+36201234567"],
    [/Címzett e-mail-címe/, "recipient@example.test"],
    [/Csomag értéke/, "15000"],
    [/Tömeg/, "1200"],
    [/Felvétel napja/, pickupDate],
  ];
  for (const [label, value] of fields) {
    const input = screen.getByLabelText(label);
    await user.click(input);
    if (input.type === "number" || input.type === "date")
      await user.type(input, value);
    else await user.paste(value);
  }
}

describe("Csomagelőjegyzés", () => {
  it("elküldi az űrlapot és megjeleníti a követési linket", async () => {
    const user = userEvent.setup();
    postData.mockResolvedValue({ trackingNumber: number });
    render(
      <MemoryRouter>
        <CreatePackage />
      </MemoryRouter>,
    );
    await fillForm(user);
    await user.click(
      screen.getByRole("button", { name: "Csomag előjegyzése" }),
    );
    expect(await screen.findByText(number)).toBeInTheDocument();
    expect(postData).toHaveBeenCalledWith(
      "Package",
      expect.objectContaining({
        weight: 1200,
        value: 15000,
        pickupDate,
        pickupSlot: "09:00-12:00",
        recipientEmail: "recipient@example.test",
      }),
    );
    expect(
      screen.getByRole("link", { name: "Csomag követése" }),
    ).toHaveAttribute("href", `/?trackingNumber=${number}`);
  });

  it("hibánál megőrzi a mezőket és megjeleníti a mezőhibát", async () => {
    const user = userEvent.setup();
    postData.mockRejectedValue(
      Object.assign(new Error("Ellenőrizd az adatokat!"), {
        status: 400,
        errors: { pickupDate: "Túl korai dátum." },
      }),
    );
    render(
      <MemoryRouter>
        <CreatePackage />
      </MemoryRouter>,
    );
    await fillForm(user);
    await user.click(
      screen.getByRole("button", { name: "Csomag előjegyzése" }),
    );
    expect(await screen.findByText("Túl korai dátum.")).toBeInTheDocument();
    expect(screen.getByLabelText(/Feladó neve/)).toHaveValue("Teszt Feladó");
    expect(
      screen.getByRole("button", { name: "Csomag előjegyzése" }),
    ).toBeEnabled();
  });

  it("mentés közben tiltja az ismételt beküldést", async () => {
    const user = userEvent.setup();
    let finish;
    postData.mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    render(
      <MemoryRouter>
        <CreatePackage />
      </MemoryRouter>,
    );
    await fillForm(user);
    await user.dblClick(
      screen.getByRole("button", { name: "Csomag előjegyzése" }),
    );
    expect(
      screen.getByRole("button", { name: "Mentés folyamatban…" }),
    ).toBeDisabled();
    expect(postData).toHaveBeenCalledTimes(1);
    finish({ trackingNumber: number });
    await screen.findByText(number);
  });

  it("üres űrlapot nem küld el", async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <CreatePackage />
      </MemoryRouter>,
    );
    await user.click(
      screen.getByRole("button", { name: "Csomag előjegyzése" }),
    );
    expect(postData).not.toHaveBeenCalled();
    expect(screen.getByText("Feladó neve kötelező!")).toBeInTheDocument();
  });

  it("mai vagy korábbi felvételi napot nem enged", async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <CreatePackage />
      </MemoryRouter>,
    );
    expect(screen.getByLabelText(/Felvétel napja/)).toHaveAttribute(
      "min",
      dayjs().add(1, "day").format("YYYY-MM-DD"),
    );
    await fillForm(user);
    const dateInput = screen.getByLabelText(/Felvétel napja/);
    await user.clear(dateInput);
    await user.type(dateInput, dayjs().format("YYYY-MM-DD"));
    await user.click(
      screen.getByRole("button", { name: "Csomag előjegyzése" }),
    );
    expect(
      screen.getByText("A felvétel legkorábban holnapra kérhető!"),
    ).toBeInTheDocument();
    expect(postData).not.toHaveBeenCalled();
  });

  it("vendégnek megjeleníti a tájékoztatót, és nem kér le profilt", () => {
    render(
      <MemoryRouter>
        <CreatePackage />
      </MemoryRouter>,
    );
    expect(screen.getByText(/Vendégként is feladhatsz/)).toBeInTheDocument();
    expect(getData).not.toHaveBeenCalled();
  });

  it("bejelentkezett felhasználónál kitölti a feladó adatait", async () => {
    useAuth.mockReturnValue({ ...customer, setAlert: vi.fn() });
    getData.mockResolvedValue({
      lastName: "Teszt",
      firstName: "Elek",
      address: "Veszprém, Fő utca 1.",
      phoneNumber: "+36301112222",
    });
    render(
      <MemoryRouter>
        <CreatePackage />
      </MemoryRouter>,
    );
    await waitFor(() =>
      expect(screen.getByLabelText(/Feladó neve/)).toHaveValue("Teszt Elek"),
    );
    expect(screen.getByLabelText(/Feladó címe/)).toHaveValue(
      "Veszprém, Fő utca 1.",
    );
    expect(screen.getByLabelText(/Feladó telefonszáma/)).toHaveValue(
      "+36301112222",
    );
    expect(getData).toHaveBeenCalledWith("User/5");
    expect(
      screen.queryByText(/Vendégként is feladhatsz/),
    ).not.toBeInTheDocument();
  });

  it("a másolás ikon a vágólapra teszi az azonosítót", async () => {
    const user = userEvent.setup();
    const setAlert = vi.fn();
    useAuth.mockReturnValue({ ...guest, setAlert });
    postData.mockResolvedValue({ trackingNumber: number });
    render(
      <MemoryRouter>
        <CreatePackage />
      </MemoryRouter>,
    );
    await fillForm(user);
    await user.click(
      screen.getByRole("button", { name: "Csomag előjegyzése" }),
    );
    await user.click(
      await screen.findByRole("button", { name: "Azonosító másolása" }),
    );
    expect(await navigator.clipboard.readText()).toBe(number);
    expect(setAlert).toHaveBeenCalledWith(
      expect.objectContaining({ severity: "success" }),
    );
  });
});

describe("Nyilvános követés", () => {
  it("a linkből automatikusan lekérdez és magyar státuszt mutat", async () => {
    getData.mockResolvedValue({
      trackingNumber: number,
      currentStatus: "PreBooked",
      changedAt: "2026-10-03T10:00:00Z",
      history: [{ status: "PreBooked", changedAt: "2026-10-03T10:00:00Z" }],
    });
    render(
      <MemoryRouter initialEntries={[`/?trackingNumber=${number}`]}>
        <Home />
      </MemoryRouter>,
    );
    expect(await screen.findByText("10. 03. 12:00")).toBeInTheDocument();
    expect(screen.getByText("Előjegyezve").closest("li")).toHaveTextContent(
      "10. 03. 12:00",
    );
    expect(getData).toHaveBeenCalledWith(
      "Package/track",
      { trackingNumber: number },
      true,
    );
  });

  it("az idővonalon csak a megtörtént állapotok kapnak időpontot", async () => {
    getData.mockResolvedValue({
      trackingNumber: number,
      currentStatus: "AtWarehouse",
      changedAt: "2026-10-05T08:30:00Z",
      history: [
        { status: "PreBooked", changedAt: "2026-10-03T10:00:00Z" },
        { status: "AtWarehouse", changedAt: "2026-10-05T08:30:00Z" },
      ],
    });
    render(
      <MemoryRouter initialEntries={[`/?trackingNumber=${number}`]}>
        <Home />
      </MemoryRouter>,
    );
    expect(await screen.findByText("10. 05. 10:30")).toBeInTheDocument();
    expect(screen.getByText("10. 03. 12:00")).toBeInTheDocument();
    expect(
      screen.getByText("Úton a raktárba").closest("li"),
    ).not.toHaveTextContent(":");
    expect(screen.getByText("Kézbesítve").closest("li")).not.toHaveTextContent(
      ":",
    );
  });

  it("keresés előtt minden állapot látszik időpont nélkül", () => {
    render(
      <MemoryRouter>
        <Home />
      </MemoryRouter>,
    );
    for (const name of [
      "Előjegyezve",
      "Úton a raktárba",
      "Raktárban",
      "Futárhoz rendelve",
      "Kézbesítve",
    ]) {
      expect(screen.getByText(name).closest("li")).not.toHaveTextContent(":");
    }
    expect(getData).not.toHaveBeenCalled();
  });

  it("üres keresésnél nem hívja a szervert", async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <Home />
      </MemoryRouter>,
    );
    await user.click(screen.getByRole("button", { name: "Keresés" }));
    expect(screen.getByText("Add meg a csomagazonosítót!")).toBeInTheDocument();
    expect(getData).not.toHaveBeenCalled();
  });

  it("ismeretlen azonosítónál a szerver üzenetét mutatja", async () => {
    const user = userEvent.setup();
    getData.mockRejectedValue(
      Object.assign(new Error("Ezzel az azonosítóval nem található csomag."), {
        status: 404,
      }),
    );
    render(
      <MemoryRouter>
        <Home />
      </MemoryRouter>,
    );
    await user.type(screen.getByLabelText("Csomagazonosító"), number);
    await user.click(screen.getByRole("button", { name: "Keresés" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "nem található csomag",
    );
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Keresés" })).toBeEnabled(),
    );
  });

  it("szerverhiba esetén a kereső alatt jelez", async () => {
    const user = userEvent.setup();
    getData.mockRejectedValue(
      new Error("A kérés nem sikerült. Ellenőrizd a szerverkapcsolatot!"),
    );
    render(
      <MemoryRouter>
        <Home />
      </MemoryRouter>,
    );
    await user.type(screen.getByLabelText("Csomagazonosító"), number);
    await user.click(screen.getByRole("button", { name: "Keresés" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Ellenőrizd a szerverkapcsolatot!",
    );
  });
});

describe("Kezdőlap", () => {
  it("vendégnek megmutatja a csempéket, a lépéseket, a tudnivalókat és a belépést", () => {
    render(
      <MemoryRouter>
        <Home />
      </MemoryRouter>,
    );
    expect(
      screen.getByRole("heading", {
        name: "Kövesd a csomagod egy kattintással",
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /Csomag feladása/ }),
    ).toHaveAttribute("href", "/csomagfeladas");
    expect(screen.getByRole("link", { name: /^Regisztráció/ })).toHaveAttribute(
      "href",
      "/register",
    );
    expect(
      screen.getByRole("heading", { name: "Hogyan működik?" }),
    ).toBeInTheDocument();
    for (const title of [
      "1. Előjegyzés",
      "2. Felvétel futárral",
      "3. Raktár",
      "4. Kézbesítés",
    ]) {
      expect(screen.getByRole("heading", { name: title })).toBeInTheDocument();
    }
    for (const chip of ["Méretek:", "Felvétel:", "Regisztrációval:"]) {
      expect(screen.getByText(chip)).toBeInTheDocument();
    }
    expect(
      screen
        .getByRole("heading", { name: "Van már fiókod?" })
        .closest("#belepes"),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Belépés" }),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Felhasználónév*")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Regisztrálj!" })).toHaveAttribute(
      "href",
      "/register",
    );
  });

  it("bejelentkezve név szerint üdvözöl, és nem kínál belépést vagy regisztrációt", () => {
    useAuth.mockReturnValue({
      ...customer,
      user: { ...customer.user, firstName: "Elek" },
      setAlert: vi.fn(),
    });
    render(
      <MemoryRouter>
        <Home />
      </MemoryRouter>,
    );
    expect(screen.getByText("Üdv, Elek!")).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /Csomag feladása/ }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: /^Regisztráció/ }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Van már fiókod?" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Belépés" }),
    ).not.toBeInTheDocument();
  });
});
