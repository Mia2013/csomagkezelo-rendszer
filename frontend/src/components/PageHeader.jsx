import { Avatar, Box, Container, Stack, Typography } from "@mui/material";

const PageHeader = ({ icon, title }) => (
  <Box sx={{ bgcolor: "primary.dark", color: "common.white" }}>
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
        <Avatar sx={{ width: 48, height: 48, bgcolor: "common.white", color: "primary.dark" }}>
          {icon}
        </Avatar>
        <Box>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 700 }}>
            {title}
          </Typography>
        </Box>
      </Stack>
    </Container>
  </Box>
);

export default PageHeader;
