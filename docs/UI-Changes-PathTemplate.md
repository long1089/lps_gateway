# UI Changes Documentation

## Report Types Management

### Index Page (`/ReportTypes/Index`)

**New Columns Added:**
1. **SFTP配置** - Displays the name of the associated SFTP configuration
   - Shows "未配置" (Not configured) if no SFTP is selected
   - Shows "未知配置" (Unknown configuration) if the SFTP config ID is invalid

2. **下载路径** - Displays the path template with code formatting
   - Shows "未配置" (Not configured) if no path template is set
   - Example display: `/data/{yyyy}/{MM}/{dd}`

**Table Layout:**
```
编码 | 名称 | 描述 | SFTP配置 | 下载路径 | 状态 | 创建时间 | 操作
```

### Create Page (`/ReportTypes/Create`)

**New Fields Added:**

1. **SFTP配置** (SFTP Configuration)
   - Type: Dropdown select
   - Options: Lists all enabled SFTP configurations
   - Format: "配置名称 (主机地址)"
   - Example: "Production SFTP (192.168.1.100)"
   - Required: No (optional selection)

2. **下载路径模板** (Download Path Template)
   - Type: Text input
   - Placeholder: `/data/{yyyy}/{MM}/{dd}`
   - Help text: 
     - 支持模板变量：{yyyy} - 年份, {MM} - 月份, {dd} - 日期, {HH} - 小时, {mm} - 分钟
     - 示例：/data/{yyyy}/{MM}/{dd} 或 /reports/{yyyy}-{MM}-{dd}/{HH}
   - Required: No (optional field)
   - Max length: 500 characters

### Edit Page (`/ReportTypes/Edit`)

Same fields as Create page, with values pre-populated from the existing record.

## SFTP Configuration Management

### Index Page (`/SftpConfigs/Index`) - **NEW**

**Columns:**
- 名称 (Name)
- 主机地址 (Host)
- 端口 (Port)
- 用户名 (Username)
- 认证类型 (Auth Type) - Badge: 密码/私钥
- 状态 (Status) - Badge: 启用/禁用
- 创建时间 (Created Time)
- 操作 (Actions) - Edit/Delete buttons (Admin only)

**Features:**
- Success/Error message display at the top
- "创建新SFTP配置" button (Admin only)
- Delete confirmation: "确定要删除此SFTP配置吗？这可能影响使用该配置的报表类型。"

### Create Page (`/SftpConfigs/Create`) - **NEW**

**Fields:**

1. **配置名称** (Name) - Required
   - Text input
   - Used for identification in report type selection

2. **主机地址** (Host) - Required
   - Text input
   - Placeholder: "192.168.1.100 或 sftp.example.com"

3. **端口** (Port) - Required
   - Number input
   - Default: 22

4. **用户名** (Username) - Required
   - Text input

5. **认证类型** (Auth Type) - Required
   - Dropdown: 密码认证 / 私钥认证
   - JavaScript toggle to show/hide password or key fields

6. **密码** (Password) - Conditional (password auth)
   - Password input
   - Only shown when auth type is "password"

7. **私钥文件路径** (Key Path) - Conditional (key auth)
   - Text input
   - Placeholder: "/path/to/private_key"
   - Help text: "私钥文件在服务器上的绝对路径"
   - Only shown when auth type is "key"

8. **私钥密码短语** (Key Passphrase) - Optional (key auth)
   - Password input
   - Help text: "如果私钥有密码保护，请输入密码"
   - Only shown when auth type is "key"

9. **并发限制** (Concurrency Limit)
   - Number input
   - Range: 1-50
   - Default: 5
   - Help text: "同时下载文件的最大数量"

10. **超时时间（秒）** (Timeout)
    - Number input
    - Range: 10-300
    - Default: 30

11. **启用** (Enabled)
    - Checkbox
    - Default: checked

**JavaScript Features:**
- Dynamic field visibility based on authentication type selection
- Form validation for required fields

### Edit Page (`/SftpConfigs/Edit`) - **NEW**

Same fields as Create page with the following differences:

1. **密码** field:
   - Placeholder: "如需更新密码请输入新密码"
   - Help text: "留空则保持原密码不变"

2. **私钥密码短语** field:
   - Placeholder: "如需更新密码短语请输入"
   - Help text: "如果私钥有密码保护，请输入密码。留空则保持不变"

## Path Template Behavior Changes

### FileDownloadJob

**Before:**
- Used `SftpConfig.BasePathTemplate` from SFTP configuration
- Called `_sftpManager.ParsePathTemplate(reportType.Code, now)`

**After:**
- Uses `ReportType.PathTemplate` from report type configuration
- Calls `_sftpManager.ParsePathTemplate(pathTemplate, now)`
- Falls back to "/" if PathTemplate is null

**Example:**
```csharp
// Old behavior
var remotePath = _sftpManager.ParsePathTemplate(reportType.Code, now);

// New behavior
var pathTemplate = reportType.PathTemplate ?? "/";
var remotePath = _sftpManager.ParsePathTemplate(pathTemplate, now);
```

## User Workflow

### Setting up a new Report Type with SFTP:

1. Navigate to `/SftpConfigs` and create an SFTP configuration (if not exists)
2. Navigate to `/ReportTypes/Create`
3. Fill in basic information (Code, Name, Description)
4. Select the SFTP configuration from the dropdown
5. Enter a path template (e.g., `/data/{yyyy}/{MM}/{dd}`)
6. Enable the report type
7. Save

### The system will:
- Validate all required fields
- Store the configuration
- Use the path template when FileDownloadJob runs
- Parse template variables based on current date/time

## Browser Compatibility

All views use:
- Bootstrap 5 for styling
- Standard HTML5 form elements
- jQuery for validation scripts
- Compatible with modern browsers (Chrome, Firefox, Safari, Edge)

## Accessibility

- All form fields have proper labels
- Required fields marked with asterisk (*)
- Help text provided for complex fields
- Success/Error messages are prominently displayed
- Confirmation dialogs for destructive actions
