import { useState } from "react";
import { Box } from "@mui/material";
import Warehouse from "@mui/icons-material/Warehouse";
import LocalShipping from "@mui/icons-material/LocalShipping";
import MoveToInbox from "@mui/icons-material/MoveToInbox";
import Inventory2 from "@mui/icons-material/Inventory2";

import Dashboard from "../components/Dashboard";
import WarehouseAssignToCourierList from "../components/Warehouse/WarehouseAssignToCourierList";
import WarehouseGetPackagesByStatus from "../components/Warehouse/WarehouseGetPackagesByStatus";

const WarehousePage = () => {
  const [selectedId, setSelectedId] = useState("pickup");
  const [counts, setCounts] = useState({});
  const [reloadKey, setReloadKey] = useState(0);

  const setCount = (id) => (count) => setCounts((prev) => ({ ...prev, [id]: count }));

  const selectCard = (id) => {
    setSelectedId(id);
    setReloadKey((prev) => prev + 1);
  };

  const options = [
    {
      id: "pickup",
      title: "Felvételre váró csomagok",
      icon: <LocalShipping />,
      count: counts.pickup ?? 0,
      render: () => <WarehouseAssignToCourierList type="pickup" onLoaded={setCount("pickup")} reloadKey={reloadKey} />,
    },
    {
      id: "delivery",
      title: "Kiszállításra váró csomagok",
      icon: <Inventory2 />,
      count: counts.delivery ?? 0,
      render: () => <WarehouseAssignToCourierList type="delivery" onLoaded={setCount("delivery")} reloadKey={reloadKey} />,
    },
    {
      id: "incoming",
      title: "Beérkező csomagok",
      icon: <MoveToInbox />,
      count: counts.incoming ?? 0,
      render: () => (
        <WarehouseGetPackagesByStatus
          status="InTransitToWarehouse"
          title="Beérkező csomagok"
          onLoaded={setCount("incoming")}
          onChanged={() => setReloadKey((prev) => prev + 1)}
          reloadKey={reloadKey}
        />
      ),
    },
  ];

  return (
    <Dashboard
      title="Raktár"
      icon={<Warehouse />}
      cards={options}
      selectedId={selectedId}
      onSelect={selectCard}
    >
      {options.map((option) => (
        <Box key={option.id} sx={{ display: option.id === selectedId ? "block" : "none" }}>
          {option.render()}
        </Box>
      ))}
    </Dashboard>
  );
};

export default WarehousePage;
