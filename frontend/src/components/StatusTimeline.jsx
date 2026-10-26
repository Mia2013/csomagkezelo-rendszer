import { Box, Stack, Typography } from "@mui/material";
import { blue } from "@mui/material/colors";
import { keyframes } from "@mui/material/styles";
import Check from "@mui/icons-material/Check";
import EditNote from "@mui/icons-material/EditNote";
import LocalShipping from "@mui/icons-material/LocalShipping";
import Warehouse from "@mui/icons-material/Warehouse";
import DeliveryDining from "@mui/icons-material/DeliveryDining";
import HomeWork from "@mui/icons-material/HomeWork";

import dayjs from "../utils/dayjs";

const statusSteps = [
  { status: "PreBooked", name: "Előjegyezve", icon: <EditNote /> },
  {
    status: "InTransitToWarehouse",
    name: "Úton a raktárba",
    icon: <LocalShipping />,
  },
  { status: "AtWarehouse", name: "Raktárban", icon: <Warehouse /> },
  {
    // Régi megoldás:
    // status: "AssignedToCourier",
    // Új megoldás:
    status: "AssignedForDelivery",
    name: "Futárhoz rendelve",
    icon: <DeliveryDining />,
  },
  { status: "Delivered", name: "Kézbesítve", icon: <HomeWork /> },
];

// A részletes backendállapotokat az öt meglévő lépéshez rendeljük.
const statusAliases = {
  AssignedForPickup: "PreBooked",
  OutForDelivery: "AssignedForDelivery",
  FailedDelivery: "AssignedForDelivery",
};
const statusLabels = {
  PreBooked: "Előjegyezve",
  AssignedForPickup: "Felvételre kiosztva",
  InTransitToWarehouse: "Úton a raktárba",
  AtWarehouse: "Raktárban",
  AssignedForDelivery: "Kiszállításra kiosztva",
  OutForDelivery: "Kiszállítás alatt",
  Delivered: "Kézbesítve",
  FailedDelivery: "Sikertelen kézbesítés",
};
const stepStatus = (status) => statusAliases[status] || status;

const pop = keyframes`
  0% { transform: scale(0.4); opacity: 0; }
  60% { transform: scale(1.15); opacity: 1; }
  100% { transform: scale(1); }
`;

const pulse = keyframes`
  0% { box-shadow: 0 0 0 0 rgba(255, 255, 255, 0.7); }
  100% { box-shadow: 0 0 0 14px rgba(255, 255, 255, 0); }
`;

const formatDate = (date) =>
  dayjs(date).tz("Europe/Budapest").format("MM. DD. HH:mm");

const StatusTimeline = ({ result }) => {
  // Régi megoldás:
  // const findEntry = (status) =>
  //   result?.history?.findLast((item) => item.status === status);
  //
  // const currentIndex = result
  //   ? statusSteps.findIndex((step) => step.status === result.currentStatus)
  //   : -1;
  // Új megoldás:
  const findEntry = (status) =>
    result?.history?.findLast((item) => item.status === status) ??
    result?.history?.findLast((item) => stepStatus(item.status) === status);

  const currentIndex = result
    ? statusSteps.findIndex((step) => step.status === stepStatus(result.currentStatus))
    : -1;

  const isDelivered = currentIndex === statusSteps.length - 1;
  const progress = isDelivered
    ? 100
    : Math.max(0, ((currentIndex + 0.5) / statusSteps.length) * 100);

  return (
    <Box sx={{ bgcolor: "rgba(255,255,255,0.10)", borderRadius: 3, p: 2 }}>
      {result && (
        <Typography role="status" variant="body2" sx={{ mb: 2 }}>
          Aktuális állapot: {statusLabels[result.currentStatus] || result.currentStatus}
        </Typography>
      )}
      <Box
        component="ol"
        sx={{
          listStyle: "none",
          m: 0,
          p: 0,
          display: "grid",
          gridTemplateColumns: `repeat(${statusSteps.length}, 1fr)`,
        }}
      >
        {statusSteps.map((step, index) => {
          const entry = findEntry(step.status);
          const isCurrent = index === currentIndex;
          return (
            <Stack
              key={step.status}
              component="li"
              aria-current={isCurrent ? "step" : undefined}
              spacing={1}
              sx={{ alignItems: "center", textAlign: "center" }}
            >
              <Box
                sx={{
                  position: "relative",
                  width: 40,
                  height: 40,
                  borderRadius: "50%",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  boxSizing: "border-box",
                  ...(entry
                    ? {
                        bgcolor: "common.white",
                        color: "primary.dark",
                        animation: `${pop} 450ms ease-out ${index * 150}ms both`,
                      }
                    : { border: `2px dashed ${blue[200]}`, color: blue[100] }),
                }}
              >
                {step.icon}
                {isCurrent && (
                  <Box
                    sx={{
                      position: "absolute",
                      inset: 0,
                      borderRadius: "50%",
                      animation: `${pulse} 2s ease-out 1s infinite`,
                    }}
                  />
                )}
                {entry && (
                  <Box
                    sx={{
                      position: "absolute",
                      right: -4,
                      bottom: -4,
                      width: 16,
                      height: 16,
                      borderRadius: "50%",
                      bgcolor: "success.main",
                      border: "2px solid white",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                    }}
                  >
                    <Check sx={{ fontSize: 12, color: "common.white" }} />
                  </Box>
                )}
              </Box>
              <Typography
                variant="caption"
                sx={{
                  fontWeight: isCurrent ? 700 : 500,
                  color: entry ? "common.white" : blue[50],
                }}
              >
                {step.name}
              </Typography>
              {entry && (
                <Typography variant="caption" sx={{ color: blue[50] }}>
                  {formatDate(entry.changedAt)}
                </Typography>
              )}
            </Stack>
          );
        })}
      </Box>
      <Box
        sx={{ height: 4, bgcolor: "primary.light", borderRadius: 1, mt: 1.5 }}
      >
        <Box
          sx={{
            height: "100%",
            width: `${progress}%`,
            bgcolor: "common.white",
            borderRadius: 1,
            transition: "width 600ms ease-out",
          }}
        />
      </Box>
    </Box>
  );
};

export default StatusTimeline;
